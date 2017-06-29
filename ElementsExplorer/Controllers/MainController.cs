using ElementsExplorer.Logging;
using ElementsExplorer.ModelBinders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ElementsExplorer.Controllers
{
	[Route("v1")]
	public class MainController : Controller
	{
		public MainController(ExplorerRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException("runtime");
			Runtime = runtime;
		}
		public ExplorerRuntime Runtime
		{
			get; set;
		}

		[HttpGet]
		[Route("utxo/{extPubKey}")]
		public string GetUTXO(
			[ModelBinder(BinderType = typeof(DestinationModelBinder))]
			BitcoinExtPubKey extPubKey)
		{
			return "hello";
		}

		[HttpGet]
		[Route("sync/{extPubKey}")]
		public bool Sync(
			[ModelBinder(BinderType = typeof(DestinationModelBinder))]
			BitcoinExtPubKey extPubKey,
			[ModelBinder(BinderType = typeof(UInt256ModelBinding))]
			uint256 lastBlockHash)
		{
			return true;
		}

		[HttpPost]
		[Route("broadcast")]
		public async Task<bool> Broadcast([FromBody]byte[] txBytes)
		{
			var tx = new Transaction(txBytes);
			try
			{
				await Runtime.RPC.SendRawTransactionAsync(tx);
				return true;
			}
			catch(RPCException ex)
			{
				Logs.Explorer.LogInformation($"Transaction {tx.GetHash()} failed to broadcast (Code: {ex.RPCCode}, Message: {ex.RPCCodeMessage}, Details: {ex.Message} )");
				return false;
			}
		}
	}
}
