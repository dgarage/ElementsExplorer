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

namespace ElementsExplorer
{
	public class ExplorerRuntime : IDisposable
	{
		public ExplorerRuntime()
		{

		}
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
				using(var node = Node.Connect(Network, configuration.NodeEndpoint))
				{
					var cts = new CancellationTokenSource();
					cts.CancelAfter(5000);
					node.VersionHandshake(cts.Token);
				}
				Logs.Configuration.LogInformation("Node connection successfull");
			}
			catch(Exception ex)
			{
				Logs.Configuration.LogError("Error while connecting to node: " + ex.Message);
			}
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
				})
				.UseUrls(ServerUrls)
				.Build();
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

		public void Dispose()
		{

		}
	}
}
