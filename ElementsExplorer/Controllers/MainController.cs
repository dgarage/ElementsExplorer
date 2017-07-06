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
		[Route("asset/{assetId}")]
		public FileContentResult GetAssetName(
			[ModelBinder(BinderType = typeof(UInt256ModelBinding))]
			uint256 assetId)
		{
			var name = Runtime.Repository.GetAssetName(assetId) ?? "";
			return new FileContentResult(Encoding.UTF8.GetBytes(name), "application/octet-stream");
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
			var actualLastBlockHash = uint256.Zero;

			var waitingTransaction = noWait ? Task.FromResult(false) : WaitingTransaction(extPubKey);

			Runtime.Repository.MarkAsUsed(new KeyInformation(extPubKey));
			UTXOChanges changes = null;
			UTXOChanges previousChanges = null;
			List<TrackedTransaction> cleanList = null;
			var getKeyPath = GetKeyPaths(extPubKey);

			while(true)
			{
				cleanList = new List<TrackedTransaction>();
				changes = new UTXOChanges();
				var transactions = Runtime.Repository
										.GetTransactions(extPubKey)
										.Select(t =>
										new
										{
											Height = GetHeight(t.BlockHash),
											Record = t
										})
										.Where(u => u.Height != OrphanHeight)
										.ToList();


				transactions = transactions
								.TopologicalSort(t =>
								{
									HashSet<uint256> dependsOn = new HashSet<uint256>(t.Record.Transaction.Inputs.Select(txin => txin.PrevOut.Hash));
									return transactions.Where(u => dependsOn.Contains(u.Record.Transaction.GetHash()) ||  //Depends on parent transaction
																	((u.Height < t.Height))); //Depends on earlier transaction
								}).ToList();

				foreach(var item in transactions)
				{
					var record = item.Record;
					if(record.BlockHash == null)
					{
						if(changes.Unconfirmed.HasConflict(record.Transaction) ||
							changes.Confirmed.HasConflict(record.Transaction))
						{
							cleanList.Add(record);
							continue;
						}
						changes.Unconfirmed.LoadChanges(record.Transaction, getKeyPath);
					}
					else
					{
						if(changes.Confirmed.HasConflict(record.Transaction))
						{
							Logs.Explorer.LogError("A conflict among confirmed transaction happened, this should be impossible");
							throw new InvalidOperationException("The impossible happened");
						}
						changes.Unconfirmed.LoadChanges(record.Transaction, getKeyPath);
						changes.Confirmed.LoadChanges(record.Transaction, getKeyPath);
						changes.Confirmed.Hash = record.BlockHash;
						actualLastBlockHash = record.BlockHash;
						if(record.BlockHash == lastBlockHash)
							previousChanges = changes.Clone();
					}
				}

				changes.Unconfirmed.Hash = changes.Unconfirmed.GetHash();
				if(changes.Unconfirmed.Hash == unconfirmedHash)
					changes.Unconfirmed.Clear();
				else
				{
					changes.Unconfirmed.Reset = true;
					changes.Unconfirmed = changes.Unconfirmed.Diff(changes.Confirmed);
				}


				if(actualLastBlockHash == lastBlockHash)
					changes.Confirmed.Clear();
				else if(previousChanges != null)
				{
					changes.Confirmed.Reset = false;
					changes.Confirmed = changes.Confirmed.Diff(previousChanges.Confirmed);
				}
				else
					changes.Confirmed.Reset = true;

				if(changes.HasChanges || !(await waitingTransaction))
					break;
				waitingTransaction = Task.FromResult(false); //next time, will not wait
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
				return Runtime.Repository.GetKeyInformation(extPubKey.ExtPubKey, script)?.KeyPath;
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
		[Route("broadcast/{extPubKey}")]
		public async Task<bool> Broadcast(
			[ModelBinder(BinderType = typeof(DestinationModelBinder))]
			BitcoinExtPubKey extPubKey = null)
		{
			var tx = new Transaction();
			var stream = new BitcoinStream(Request.Body, false);
			tx.ReadWrite(stream);
			try
			{
				await Runtime.RPC.SendRawTransactionAsync(tx);
				return true;
			}
			catch(RPCException ex)
			{
				Logs.Explorer.LogInformation($"Transaction {tx.GetHash()} failed to broadcast (Code: {ex.RPCCode}, Message: {ex.RPCCodeMessage}, Details: {ex.Message} )");
				if(extPubKey != null && ex.Message.StartsWith("Missing inputs", StringComparison.OrdinalIgnoreCase))
				{
					Logs.Explorer.LogInformation("Trying to broadcast unconfirmed of the wallet");
					var transactions = Runtime.Repository.GetTransactions(extPubKey);
					foreach(var existing in transactions)
					{
						var height = GetHeight(existing.BlockHash);
						if(height == MempoolHeight || height == OrphanHeight)
						{
							try
							{
								await Runtime.RPC.SendRawTransactionAsync(existing.Transaction);
							}
							catch { }
						}
					}

					try
					{

						await Runtime.RPC.SendRawTransactionAsync(tx);
						Logs.Explorer.LogInformation($"Broadcast success");
						return true;
					}
					catch(RPCException)
					{
						Logs.Explorer.LogInformation($"Transaction {tx.GetHash()} failed to broadcast (Code: {ex.RPCCode}, Message: {ex.RPCCodeMessage}, Details: {ex.Message} )");
					}
				}
				return false;
			}
		}
	}
}
