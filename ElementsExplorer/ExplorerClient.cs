using NBitcoin;
using NBitcoin.JsonConverters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ElementsExplorer
{
	public class ExplorerClient
	{
		public ExplorerClient(Network network, Uri serverAddress)
		{
			if(serverAddress == null)
				throw new ArgumentNullException(nameof(serverAddress));
			if(network == null)
				throw new ArgumentNullException(nameof(network));
			_Address = serverAddress;
			_Network = network;
		}

		public UTXOChanges Sync(BitcoinExtPubKey extKey, uint256 lastBlockHash)
		{
			return SyncAsync(extKey, lastBlockHash).GetAwaiter().GetResult();
		}

		public async Task<UTXOChanges> SyncAsync(BitcoinExtPubKey extKey, uint256 lastBlockHash)
		{
			var bytes = await SendAsync<byte[]>(HttpMethod.Get, null, "v1/sync/{0}?lastBlockHash={1}", extKey, lastBlockHash).ConfigureAwait(false);
			UTXOChanges changes = new UTXOChanges();
			changes.FromBytes(bytes);
			return changes;
		}

		public string GetUTXOs(BitcoinExtPubKey extKey)
		{
			if(extKey == null)
				throw new ArgumentNullException("extKey");
			return GetUTXOsAsync(extKey).GetAwaiter().GetResult();
		}

		public bool Broadcast(Transaction tx)
		{
			return BroadcastAsync(tx).GetAwaiter().GetResult();
		}

		public Task<bool> BroadcastAsync(Transaction tx)
		{
			return SendAsync<bool>(HttpMethod.Post, tx.ToBytes(), "v1/broadcast");
		}

		public Task<string> GetUTXOsAsync(BitcoinExtPubKey extKey)
		{
			return SendAsync<string>(HttpMethod.Get, null, "v1/utxo/{0}", extKey);
		}

		private static readonly HttpClient SharedClient = new HttpClient();
		internal HttpClient Client = SharedClient;

		private readonly Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
		}


		private readonly Uri _Address;
		public Uri Address
		{
			get
			{
				return _Address;
			}
		}


		private string GetFullUri(string relativePath, params object[] parameters)
		{
			relativePath = String.Format(relativePath, parameters ?? new object[0]);
			var uri = Address.AbsoluteUri;
			if(!uri.EndsWith("/", StringComparison.Ordinal))
				uri += "/";
			uri += relativePath;
			return uri;
		}
		private Task<T> GetAsync<T>(string relativePath, params object[] parameters)
		{
			return SendAsync<T>(HttpMethod.Get, null, relativePath, parameters);
		}
		private async Task<T> SendAsync<T>(HttpMethod method, object body, string relativePath, params object[] parameters)
		{
			var uri = GetFullUri(relativePath, parameters);
			var message = new HttpRequestMessage(method, uri);
			if(body != null)
			{
				message.Content = new StringContent(Serializer.ToString(body, Network), Encoding.UTF8, "application/json");
			}
			var result = await Client.SendAsync(message).ConfigureAwait(false);
			if(result.StatusCode == HttpStatusCode.NotFound)
				return default(T);
			if(!result.IsSuccessStatusCode)
			{
				string error = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
				if(!string.IsNullOrEmpty(error))
				{
					throw new HttpRequestException(result.StatusCode + ": " + error);
				}
			}
			result.EnsureSuccessStatusCode();
			if(typeof(T) == typeof(byte[]))
				return (T)(object)await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
			if(typeof(T) == typeof(string))
				return (T)(object)str;
			return Serializer.ToObject<T>(str, Network);
		}

		public void GetUTXOs()
		{
			throw new NotImplementedException();
		}
	}
}
