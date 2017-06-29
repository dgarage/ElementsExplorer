using ElementsExplorer.Configuration;
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
			Logs.Configuration.LogInformation("Trying to connect to node: " + configuration.NodeEndpoint);
			try
			{
				if(!configuration.RPC.NoTest)
				{
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

			Chain = new ConcurrentChain(Network.GetGenesis().Header);
			_Nodes = CreateNodeGroup(Chain);
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
			}
		}
	}
}
