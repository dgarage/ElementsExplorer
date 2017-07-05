using ElementsExplorer.Logging;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NBitcoin.Crypto;
using Completion = System.Threading.Tasks.TaskCompletionSource<bool>;

namespace ElementsExplorer
{
	public class ExplorerBehavior : NodeBehavior
	{
		public ExplorerBehavior(ExplorerRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException("runtime");
			_Runtime = runtime;
		}

		private readonly ExplorerRuntime _Runtime;
		public ExplorerRuntime Runtime
		{
			get
			{
				return _Runtime;
			}
		}
		public override object Clone()
		{
			return new ExplorerBehavior(Runtime);
		}

		Timer _Timer;

		protected override void AttachCore()
		{
			AttachedNode.StateChanged += AttachedNode_StateChanged;
			AttachedNode.MessageReceived += AttachedNode_MessageReceived;
			_CurrentLocation = Runtime.Repository.GetIndexProgress();
			_Timer = new Timer(Tick, null, 0, 30);
		}


		public async Task WaitFor(ExtPubKey pubKey, CancellationToken cancellation = default(CancellationToken))
		{
			TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();

			var key = Hashes.Hash160(pubKey.ToBytes());

			lock(_WaitFor)
			{
				_WaitFor.Add(key, completion);
			}

			cancellation.Register(() =>
			{
				completion.TrySetCanceled();
			});

			try
			{
				await completion.Task;
			}
			finally
			{
				lock(_WaitFor)
				{
					_WaitFor.Remove(key, completion);
				}
			}
		}

		MultiValueDictionary<uint160, Completion> _WaitFor = new MultiValueDictionary<uint160, Completion>();

		public void AskBlocks()
		{
			if(AttachedNode.State != NodeState.HandShaked)
				return;
			var pendingTip = AttachedNode.Behaviors.Find<ChainBehavior>().PendingTip;
			if(pendingTip == null || pendingTip.Height < AttachedNode.PeerVersion.StartHeight)
				return;
			if(_InFlights.Count != 0)
				return;
			var currentLocation = _CurrentLocation ?? new BlockLocator() { Blocks = { Runtime.Network.GenesisHash } };
			var chainFork = Runtime.Chain.FindFork(currentLocation);

			//Up to date
			if(pendingTip.HashBlock == currentLocation.Blocks[0])
				return;

			var toDownload = pendingTip.EnumerateToGenesis().TakeWhile(b => b.HashBlock != currentLocation.Blocks[0]).ToArray();
			Array.Reverse(toDownload);
			var invs = toDownload.Take(10)
				.Select(b => new InventoryVector(AttachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), b.HashBlock))
				.Where(b => _InFlights.TryAdd(b.Hash, new Download()))
				.ToArray();

			if(invs.Length != 0)
			{
				AttachedNode.SendMessageAsync(new GetDataPayload(invs));
				Runtime.Repository.SetIndexProgress(currentLocation);
			}
		}

		class Download
		{
		}

		ConcurrentDictionary<uint256, Download> _InFlights = new ConcurrentDictionary<uint256, Download>();


		void Tick(object state)
		{
			try
			{
				AskBlocks();
			}
			catch(Exception ex)
			{
				if(AttachedNode == null)
					return;
				Logs.Explorer.LogError("Exception in ExplorerBehavior tick loop");
				Logs.Explorer.LogError(ex.ToString());
			}
		}



		BlockLocator _CurrentLocation;

		protected override void DetachCore()
		{
			AttachedNode.StateChanged -= AttachedNode_StateChanged;
			AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
			_Timer.Dispose();
			_Timer = null;
		}

		private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
		{
			message.Message.IfPayloadIs<InvPayload>(invs =>
			{
				var data = new GetDataPayload();
				foreach(var inv in invs.Inventory)
				{
					inv.Type = node.AddSupportedOptions(inv.Type);
					if(inv.Type.HasFlag(InventoryType.MSG_TX))
						data.Inventory.Add(inv);
				}
				if(data.Inventory.Count != 0)
					node.SendMessageAsync(data);
			});

			message.Message.IfPayloadIs<HeadersPayload>(headers =>
			{
				if(headers.Headers.Count == 0)
					return;
				AskBlocks();
			});

			message.Message.IfPayloadIs<BlockPayload>(block =>
			{
				block.Object.Header.CacheHashes();
				Download o;
				if(_InFlights.TryRemove(block.Object.GetHash(), out o))
				{
					HashSet<ExtPubKey> pubKeys = new HashSet<ExtPubKey>();
					using(var db = Runtime.Repository.CreateTransaction())
					{
						foreach(var tx in block.Object.Transactions)
						{
							var pubKeys2 = GetInterestedWallet(tx);
							foreach(var pubkey in pubKeys2)
							{
								pubKeys.Add(pubkey);
								db.InsertTransaction(pubkey, block.Object.GetHash(), tx);
							}
						}
						var blockHeader = Runtime.Chain.GetBlock(block.Object.GetHash());
						if(blockHeader != null)
						{
							_CurrentLocation = blockHeader.GetLocator();
							Logs.Explorer.LogInformation($"Processed block {block.Object.GetHash()}");
						}
						db.Commit();

						foreach(var tx in block.Object.Transactions)
							ScanForAssetName(tx, false);
					}
					foreach(var pubkey in pubKeys)
					{
						Notify(pubkey, false);
					}
				}
				if(_InFlights.Count == 0)
					AskBlocks();
			});

			message.Message.IfPayloadIs<TxPayload>(txPayload =>
			{
				var pubKeys = GetInterestedWallet(txPayload.Object);
				using(var db = Runtime.Repository.CreateTransaction())
				{
					foreach(var pubkey in pubKeys)
					{
						db.InsertTransaction(pubkey, null, txPayload.Object);
					}
					db.Commit();
				}
				ScanForAssetName(txPayload.Object, true);
				foreach(var pubkey in pubKeys)
				{
					Notify(pubkey, true);
				}
			});

		}

		private void ScanForAssetName(Transaction tx, bool logFailure)
		{
			var name = NamedIssuance.Extract(tx);
			if(name != null)
			{
				var result = Runtime.Repository.SetAssetName(name);
				if(result == Repository.SetNameResult.Success)
					Logs.Explorer.LogInformation($"Name {name.Name} claimed by {name.AssetId}");
				else
					if(logFailure)
						Logs.Explorer.LogInformation($"Name {name.Name} failed to be claimed by {name.AssetId}, cause: {result}");
			}
		}

		private void Notify(ExtPubKey pubkey, bool log)
		{
			if(log)
				Logs.Explorer.LogInformation($"A wallet received money");
			var key = Hashes.Hash160(pubkey.ToBytes());
			lock(_WaitFor)
			{
				IReadOnlyCollection<Completion> completions;
				if(_WaitFor.TryGetValue(key, out completions))
				{
					foreach(var completion in completions.ToList())
					{
						completion.TrySetResult(true);
					}
				}
			}
		}

		private HashSet<ExtPubKey> GetInterestedWallet(Transaction tx)
		{
			var pubKeys = new HashSet<ExtPubKey>();
			tx.CacheHashes();
			foreach(var input in tx.Inputs)
			{
				var signer = input.ScriptSig.GetSigner() ?? input.WitScript.ToScript().GetSigner();
				if(signer != null)
				{
					var keyInfo = Runtime.Repository.GetKeyInformation(signer.ScriptPubKey);
					if(keyInfo != null)
						pubKeys.Add(new ExtPubKey(keyInfo.RootKey));
				}
			}

			foreach(var output in tx.Outputs)
			{
				var keyInfo = Runtime.Repository.GetKeyInformation(output.ScriptPubKey);
				if(keyInfo != null)
					pubKeys.Add(new ExtPubKey(keyInfo.RootKey));
			}
			return pubKeys;
		}

		private void AttachedNode_StateChanged(Node node, NodeState oldState)
		{
			if(node.State == NodeState.HandShaked)
			{
				Logs.Explorer.LogInformation($"Handshaked Elements node");
				node.SendMessageAsync(new SendHeadersPayload());
				AskBlocks();
			}
			if(node.State == NodeState.Offline)
				Logs.Explorer.LogInformation($"Closed connection with Elements node");
			if(node.State == NodeState.Failed)
				Logs.Explorer.LogError($"Connection with Elements unexpectedly failed: {node.DisconnectReason.Reason}");
		}
	}
}
