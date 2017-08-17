﻿using ElementsExplorer.Configuration;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.RPC;
using NBitcoin.Protocol;
using System.Threading;
using ElementsExplorer.Logging;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using NBitcoin.Protocol.Behaviors;
using System.IO;
using System.Threading.Tasks;

namespace ElementsExplorer
{
	public class ExplorerRuntime : IDisposable
	{
		public ExplorerRuntime()
		{

		}
		NodesGroup _Nodes;
		public ExplorerRuntime(ExplorerConfiguration configuration)
		{
			if(configuration == null)
				throw new ArgumentNullException("configuration");
			Network = configuration.Network;
			Chain = new ConcurrentChain(Network.GetGenesis().Header);
			RPC = configuration.RPC.ConfigureRPCClient(configuration.Network);
			NodeEndpoint = configuration.NodeEndpoint;
			ServerUrls = configuration.GetUrls();

			var cachePath = Path.Combine(configuration.DataDir, "chain.dat");
			if(configuration.CacheChain)
			{
				Logs.Configuration.LogInformation($"Loading chain from cache...");
				if(File.Exists(cachePath))
				{
					Chain.Load(File.ReadAllBytes(cachePath));
				}
			}

			Logs.Configuration.LogInformation($"Loading chain from node...");
			var heightBefore = Chain.Height;
			try
			{
				if(!configuration.RPC.NoTest)
				{
					Logs.Configuration.LogInformation("Trying to connect to node: " + configuration.NodeEndpoint);
					using(var node = Node.Connect(Network, configuration.NodeEndpoint))
					{
						var cts = new CancellationTokenSource();
						cts.CancelAfter(5000);
						node.VersionHandshake(cts.Token);
						node.SynchronizeChain(Chain);
					}
					Logs.Configuration.LogInformation("Node connection successfull");
				}
			}
			catch(Exception ex)
			{
				Logs.Configuration.LogError("Error while connecting to node: " + ex.Message);
				throw new ConfigException();
			}

			Logs.Configuration.LogInformation($"Chain loaded from node");

			if(configuration.CacheChain && heightBefore != Chain.Height)
			{
				Logs.Configuration.LogInformation($"Saving chain to cache...");
				var ms = new MemoryStream();
				Chain.WriteTo(ms);
				File.WriteAllBytes(cachePath, ms.ToArray());
			}

			var dbPath = Path.Combine(configuration.DataDir, "db");
			Repository = new Repository(dbPath, true);
			if(configuration.Rescan)
			{
				Logs.Configuration.LogInformation("Rescanning...");
				Repository.SetIndexProgress(null);
			}
		}

		public void StartNodeListener(int startHeight)
		{
			_Nodes = CreateNodeGroup(Chain, startHeight);
			while(_Nodes.ConnectedNodes.Count == 0)
				Thread.Sleep(10);
		}

		public Repository Repository
		{
			get; set;
		}

		public async Task<bool> WaitFor(ExtPubKey extPubKey, CancellationToken token)
		{
			var node = _Nodes.ConnectedNodes.FirstOrDefault();
			if(node == null)
				return false;
			await node.Behaviors.Find<ExplorerBehavior>().WaitFor(extPubKey, token).ConfigureAwait(false);
			return true;
		}

		public ConcurrentChain Chain
		{
			get; set;
		}

		public string[] ServerUrls
		{
			get; set;
		}

		public IWebHost CreateWebHost()
		{
			return new WebHostBuilder()
				.UseKestrel()
				.UseStartup<Startup>()
				.ConfigureServices(services =>
				{
					services.AddSingleton(provider =>
					{
						return this;
					});
					services.AddSingleton(Network);
				})
				.UseUrls(ServerUrls)
				.Build();
		}

		NodesGroup CreateNodeGroup(ConcurrentChain chain, int startHeight)
		{
			AddressManager manager = new AddressManager();
			manager.Add(new NetworkAddress(NodeEndpoint), IPAddress.Loopback);
			NodesGroup group = new NodesGroup(Network, new NodeConnectionParameters()
			{
				Services = NodeServices.Nothing,
				IsRelay = true,
				TemplateBehaviors =
				{
					new AddressManagerBehavior(manager)
					{
						PeersToDiscover = 1,
						Mode = AddressManagerBehaviorMode.None
					},
					new ExplorerBehavior(this, chain) { StartHeight = startHeight },
					new ChainBehavior(chain)
					{
						CanRespondToGetHeaders = false
					}
				}
			});
			group.AllowSameGroup = true;
			group.MaximumNodeConnection = 1;
			group.Connect();
			return group;
		}


		public Network Network
		{
			get; set;
		}
		public RPCClient RPC
		{
			get;
			set;
		}
		public IPEndPoint NodeEndpoint
		{
			get;
			set;
		}

		object l = new object();
		public void Dispose()
		{
			lock(l)
			{

				if(_Nodes != null)
				{
					_Nodes.Disconnect();
					_Nodes = null;
				}
				if(Repository != null)
				{
					Repository.Dispose();
					Repository = null;
				}
			}
		}
	}
}
