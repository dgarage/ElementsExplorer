using ElementsExplorer.Configuration;
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
			RPC = configuration.RPC.ConfigureRPCClient(configuration.Network);
			NodeEndpoint = configuration.NodeEndpoint;
			ServerUrls = configuration.GetUrls();			
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
					}
					Logs.Configuration.LogInformation("Node connection successfull");
				}
			}
			catch(Exception ex)
			{
				Logs.Configuration.LogError("Error while connecting to node: " + ex.Message);
			}

			var dbPath = Path.Combine(configuration.DataDir, "db");
			Repository = new Repository(dbPath, true);
			Chain = new ConcurrentChain(Network.GetGenesis().Header);			
		}

		public void StartNodeListener()
		{
			using(var node = Node.Connect(Network, NodeEndpoint))
			{
				var cts = new CancellationTokenSource();
				cts.CancelAfter(5000);
				node.VersionHandshake(cts.Token);
				Chain = node.GetChain();
			}
			_Nodes = CreateNodeGroup(Chain);
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

		NodesGroup CreateNodeGroup(ConcurrentChain chain)
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
					new ExplorerBehavior(this),
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
