using ElementsExplorer.Logging;
using ElementsExplorer.ModelBinders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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
		[Route("sync/{extPubKey}")]
		public async Task<FileContentResult> Sync(
			[ModelBinder(BinderType = typeof(DestinationModelBinder))]
			BitcoinExtPubKey extPubKey,
			[ModelBinder(BinderType = typeof(UInt256ModelBinding))]
			uint256 lastBlockHash = null,
			[ModelBinder(BinderType = typeof(UInt256ModelBinding))]
			uint256 unconfirmedHash = null,
			bool noWait = false)
		{
			lastBlockHash = lastBlockHash ?? uint256.Zero;

			Runtime.Repository.MarkAsUsed(new KeyInformation(extPubKey));
			UTXOChanges changes = null;
			UTXOChanges previousChanges = null;
			List<TrackedTransaction> cleanList = null;

			while(true)
			{
				cleanList = new List<TrackedTransaction>();
				changes = new UTXOChanges();
				var transactions = Runtime.Repository.GetTransactions(extPubKey);

				var lastBlock = Runtime.Chain.GetBlock(lastBlockHash);
				transactions = transactions.OrderBy(t => GetHeight(t.BlockHash)).ToArray();
				foreach(var transaction in transactions)
				{
					int height = GetHeight(transaction.BlockHash);
					if(height == OrphanHeight)
					{
						cleanList.Add(transaction);
						continue;
					}

					if(transaction.BlockHash != null)
						changes.BlockHash = transaction.BlockHash;

					UTXOChange utxo = null;
					if(transaction.BlockHash == null)
					{
						utxo = changes.Unconfirmed;
						if(utxo.HasConflict(transaction.Transaction) ||
							changes.Confirmed.HasConflict(transaction.Transaction))
						{
							cleanList.Add(transaction);
							continue;
						}
					}
					else
					{
						utxo = changes.Confirmed;
						if(utxo.HasConflict(transaction.Transaction))
						{
							Logs.Explorer.LogError("A conflict among confirmed transaction happened, this should be impossible");
							throw new InvalidOperationException("The impossible happened");
						}
					}

					utxo.LoadChanges(transaction.Transaction, GetKeyPaths(extPubKey));


					if(transaction.BlockHash == lastBlockHash)
						previousChanges = changes.Clone();
				}


				changes.UnconfirmedHash = changes.Unconfirmed.GetHash();
				if(changes.UnconfirmedHash == unconfirmedHash)
					changes.Unconfirmed = new UTXOChange();

				changes.Reset = previousChanges == null;

				if(previousChanges != null)
				{
					changes.Confirmed = changes.Confirmed.Diff(previousChanges.Confirmed);
				}

				if(noWait || changes.HasChange || !(await WaitingTransaction(extPubKey)))
					break;
			}

			Runtime.Repository.CleanTransactions(extPubKey.ExtPubKey, cleanList);

			return new FileContentResult(changes.ToBytes(), "application/octet-stream");

		}

		private async Task<bool> WaitingTransaction(BitcoinExtPubKey extPubKey)
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			int timeout = 10000;
			cts.CancelAfter(timeout);

			try
			{
				if(!await Runtime.WaitFor(extPubKey.ExtPubKey, cts.Token))
				{
					await Task.Delay(timeout);
					return false;
				}
			}
			catch(OperationCanceledException) { return false; }
			return true;
		}

		private Func<Script, KeyPath> GetKeyPaths(BitcoinExtPubKey extPubKey)
		{
			return (script) =>
			{
				return Runtime.Repository.GetKeyInformation(script)?.KeyPath;
			};
		}

		const int MempoolHeight = int.MaxValue;
		const int OrphanHeight = int.MaxValue - 1;
		private int GetHeight(uint256 blockHash)
		{
			if(blockHash == null)
				return MempoolHeight;
			var header = Runtime.Chain.GetBlock(blockHash);
			return header == null ? OrphanHeight : header.Height;
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
