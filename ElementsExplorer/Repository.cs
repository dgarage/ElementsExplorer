using DBreeze;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.JsonConverters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ElementsExplorer
{
	public class KeyInformation
	{
		public KeyInformation()
		{

		}
		public KeyInformation(ExtPubKey pubKey) : this(pubKey, null)
		{

		}
		public KeyInformation(ExtPubKey pubKey, KeyPath keyPath)
		{
			KeyPath = keyPath;
			RootKey = pubKey.ToBytes();
		}
		public byte[] RootKey
		{
			get; set;
		}
		public KeyPath KeyPath
		{
			get; set;
		}
	}
	public class Repository : IDisposable
	{
		DBreezeEngine _Engine;
		public Repository(string directory, bool caching)
		{
			if(!Directory.Exists(directory))
				Directory.CreateDirectory(directory);
			_Engine = new DBreezeEngine(directory);

			Caching = caching;
			if(caching)
			{
				using(var tx = _Engine.GetTransaction())
				{
					tx.ValuesLazyLoadingIsOn = false;
					foreach(var existingRow in tx.SelectForward<string, byte[]>("KeysByScript"))
					{
						if(existingRow == null || !existingRow.Exists)
							continue;
						_Cache.TryAdd(new ScriptId(existingRow.Key), Serializer.ToObject<KeyInformation>(Unzip(existingRow.Value)));
					}
				}
			}
		}

		public KeyInformation GetKeyInformation(Script script)
		{
			if(Caching)
				return _Cache.TryGet(script.Hash);
			using(var tx = _Engine.GetTransaction())
			{
				tx.ValuesLazyLoadingIsOn = false;
				var existingRow = tx.Select<string, byte[]>("KeysByScript", script.Hash.ToString());
				if(existingRow == null || !existingRow.Exists)
					return null;
				return Serializer.ToObject<KeyInformation>(Unzip(existingRow.Value));
			}
		}

		private byte[] Zip(string unzipped)
		{
			MemoryStream ms = new MemoryStream();
			using(GZipStream gzip = new GZipStream(ms, CompressionMode.Compress))
			{
				StreamWriter writer = new StreamWriter(gzip, Encoding.UTF8);
				writer.Write(unzipped);
				writer.Flush();
			}
			return ms.ToArray();
		}
		private string Unzip(byte[] bytes)
		{
			MemoryStream ms = new MemoryStream(bytes);
			using(GZipStream gzip = new GZipStream(ms, CompressionMode.Decompress))
			{
				StreamReader reader = new StreamReader(gzip, Encoding.UTF8);
				var unzipped = reader.ReadToEnd();
				return unzipped;
			}
		}

		public const int MinGap = 20;
		readonly KeyPath[] TrackedPathes = new KeyPath[] { new KeyPath("0"), new KeyPath("1") };
		public void MarkAsUsed(KeyInformation info)
		{
			var tableName = $"U-{Hashes.Hash160(info.RootKey).ToString()}";
			var highestUsedIndexes = new Dictionary<KeyPath, long>();
			var highestUnusedIndexes = new Dictionary<KeyPath, long>();
			using(var tx = _Engine.GetTransaction())
			{
				tx.ValuesLazyLoadingIsOn = false;
				if(info.KeyPath != null)
					tx.Insert(tableName, info.KeyPath.ToString(), true);

				foreach(var row in tx.SelectForward<string, bool>(tableName))
				{
					if(info.KeyPath == null)
						return; //Early exit, no need to create the first keys, it has already been done
					var highestIndexes = row.Value ? highestUsedIndexes : highestUnusedIndexes;
					KeyPath k = new KeyPath(row.Key);
					long highestKey;
					if(!highestIndexes.TryGetValue(k.Parent, out highestKey))
						highestKey = -1;
					highestKey = Math.Max(highestKey, k.Indexes.Last());
					highestIndexes.AddOrReplace(k.Parent, highestKey);
				}

				foreach(var trackedPath in TrackedPathes)
				{
					ExtPubKey pathPubKey = null;
					long highestUnused;
					if(!highestUnusedIndexes.TryGetValue(trackedPath, out highestUnused))
						highestUnused = -1;

					long highestUsed;
					if(!highestUsedIndexes.TryGetValue(trackedPath, out highestUsed))
						highestUsed = -1;

					KeyPath highestUnusedPath = null;
					while(highestUnused - highestUsed < MinGap)
					{
						if(highestUnused == uint.MaxValue)
							break;
						highestUnused++;

						highestUnusedPath = trackedPath.Derive((uint)highestUnused);
						pathPubKey = pathPubKey ?? new ExtPubKey(info.RootKey).Derive(trackedPath);

						var scriptPubKey = pathPubKey.Derive((uint)highestUnused).PubKey.Hash.ScriptPubKey;
						InsertKeyInformation(tx, scriptPubKey, new KeyInformation()
						{
							KeyPath = trackedPath.Derive((uint)highestUnused),
							RootKey = info.RootKey
						});
					}

					if(highestUnusedPath != null)
					{
						byte[] inserted;
						bool existed;
						tx.Insert(tableName, highestUnusedPath.ToString(), false, out inserted, out existed, dontUpdateIfExists: true);
					}
				}
				tx.Commit();
			}
		}

		public bool Caching
		{
			get;
			private set;
		}

		ConcurrentDictionary<ScriptId, KeyInformation> _Cache = new ConcurrentDictionary<ScriptId, KeyInformation>();

		private void InsertKeyInformation(DBreeze.Transactions.Transaction tx, Script scriptPubKey, KeyInformation info)
		{
			if(Caching)
				_Cache.TryAdd(scriptPubKey.Hash, info);
			tx.Insert("KeysByScript", scriptPubKey.Hash.ToString(), Zip(Serializer.ToString(info)));
		}

		public void Dispose()
		{
			_Engine.Dispose();
		}
	}
}
