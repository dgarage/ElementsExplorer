using ElementsExplorer.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using NBitcoin.RPC;

namespace ElementsExplorer.Tests
{
	public class UnitTest1
	{
		public UnitTest1(ITestOutputHelper output)
		{
			Logs.Configure(new TestOutputHelperFactory(output));
		}

		[Fact]
		public void RepositoryCanTrackAddresses()
		{
			using(var tester = RepositoryTester.Create(true))
			{
				RepositoryCanTrackAddresses(tester);
				tester.ReloadRepository(true);
				var keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/26")).PubKey.Hash.ScriptPubKey);
				Assert.NotNull(keyInfo);
				Assert.Equal(new KeyPath("1/26"), keyInfo.KeyPath);
				Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));
				keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/27")).PubKey.Hash.ScriptPubKey);
				Assert.Null(keyInfo);

			}
			using(var tester = RepositoryTester.Create(false))
			{
				RepositoryCanTrackAddresses(tester);
			}
		}

		static ExtPubKey pubKey = new ExtKey().Neuter();
		private static void RepositoryCanTrackAddresses(RepositoryTester tester)
		{

			tester.Repository.MarkAsUsed(new KeyInformation(pubKey));
			var keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("0/0")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("0/0"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));

			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("0/1")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("0/1"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));

			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("0/19")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("0/19"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));


			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/19")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("1/19"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));

			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("0/20")).PubKey.Hash.ScriptPubKey);
			Assert.Null(keyInfo);
			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/20")).PubKey.Hash.ScriptPubKey);
			Assert.Null(keyInfo);

			tester.Repository.MarkAsUsed(new KeyInformation(pubKey, new KeyPath("1/5")));
			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/25")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("1/25"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));

			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/26")).PubKey.Hash.ScriptPubKey);
			Assert.Null(keyInfo);

			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("0/20")).PubKey.Hash.ScriptPubKey);
			Assert.Null(keyInfo);


			tester.Repository.MarkAsUsed(new KeyInformation(pubKey, new KeyPath("1/6")));
			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/26")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("1/26"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));
			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/27")).PubKey.Hash.ScriptPubKey);
			Assert.Null(keyInfo);

			//No op
			tester.Repository.MarkAsUsed(new KeyInformation(pubKey, new KeyPath("1/6")));
			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/26")).PubKey.Hash.ScriptPubKey);
			Assert.NotNull(keyInfo);
			Assert.Equal(new KeyPath("1/26"), keyInfo.KeyPath);
			Assert.True(keyInfo.RootKey.SequenceEqual(pubKey.ToBytes()));
			keyInfo = tester.Repository.GetKeyInformation(pubKey.Derive(new KeyPath("1/27")).PubKey.Hash.ScriptPubKey);
			Assert.Null(keyInfo);
		}


		[Fact]
		public void CanTrack2()
		{
			using(var tester = ServerTester.Create())
			{
				var key = new BitcoinExtKey(new ExtKey(), tester.Runtime.Network);
				tester.Client.Sync(key.Neuter(), null, null, true); //Track things do not wait
				tester.Runtime.RPC.SendToAddress(AddressOf(key, "0/0"), Money.Coins(1.0m));
				var utxo = tester.Client.Sync(key.Neuter(), null, null);
				Assert.True(utxo.Reset);
				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);


				var randomDude = new Key();
				LockTestCoins(tester.Runtime.RPC);
				tester.Runtime.RPC.ImportPrivKey(PrivateKeyOf(key, "0/0"));
				tester.Runtime.RPC.SendToAddress(AddressOf(key, "1/0"), Money.Coins(0.6m));

				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);

				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(0, utxo.Unconfirmed.SpentOutpoints.Count);
			}
		}

		[Fact]
		public void CanTrack()
		{
			using(var tester = ServerTester.Create())
			{
				var key = new BitcoinExtKey(new ExtKey(), tester.Runtime.Network);
				tester.Client.Sync(key.Neuter(), null, null, true); //Track things do not wait
				var gettingUTXO = tester.Client.SyncAsync(key.Neuter(), null, null);
				var txId = tester.Runtime.RPC.SendToAddress(AddressOf(key, "0/0"), Money.Coins(1.0m));
				var utxo = gettingUTXO.GetAwaiter().GetResult();


				Assert.True(utxo.Reset);
				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(0, utxo.Confirmed.UTXOs.Count);
				Assert.Equal(uint256.Zero, utxo.BlockHash);
				Assert.Equal(utxo.Unconfirmed.GetHash(), utxo.UnconfirmedHash);

				tester.Runtime.RPC.Generate(1);
				var prevUtxo = utxo;
				utxo = tester.Client.Sync(key.Neuter(), prevUtxo.BlockHash, prevUtxo.UnconfirmedHash);
				Assert.Equal(0, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(1, utxo.Confirmed.UTXOs.Count);
				var bestBlockHash = tester.Runtime.RPC.GetBestBlockHash();
				Assert.Equal(bestBlockHash, utxo.BlockHash);
				Assert.Equal(utxo.Unconfirmed.GetHash(), utxo.UnconfirmedHash);

				txId = tester.Runtime.RPC.SendToAddress(AddressOf(key, "0/1"), Money.Coins(1.0m));

				prevUtxo = utxo;
				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);
				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(0, utxo.Confirmed.UTXOs.Count);
				utxo = tester.Client.Sync(key.Neuter(), null, null, true);

				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/1"), utxo.Unconfirmed.UTXOs[0].KeyPath);
				Assert.Equal(1, utxo.Confirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/0"), utxo.Confirmed.UTXOs[0].KeyPath);
				Assert.Equal(bestBlockHash, utxo.BlockHash);
				Assert.Equal(utxo.Unconfirmed.GetHash(), utxo.UnconfirmedHash);

				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, null, true);
				Assert.False(utxo.Reset);
				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(0, utxo.Confirmed.UTXOs.Count);

				utxo = tester.Client.Sync(key.Neuter(), null, utxo.UnconfirmedHash, true);
				Assert.True(utxo.Reset);
				Assert.Equal(0, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(1, utxo.Confirmed.UTXOs.Count);
				tester.Runtime.RPC.Generate(1);

				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);
				Assert.Equal(1, utxo.Confirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/1"), utxo.Confirmed.UTXOs[0].KeyPath);

				var outpoint01 = utxo.Confirmed.UTXOs[0].Outpoint;

				txId = tester.Runtime.RPC.SendToAddress(AddressOf(key, "0/2"), Money.Coins(1.0m));
				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);
				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/2"), utxo.Unconfirmed.UTXOs[0].KeyPath);
				tester.Runtime.RPC.Generate(1);

				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);
				Assert.Equal(1, utxo.Confirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/2"), utxo.Confirmed.UTXOs[0].KeyPath);


				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash, true);
				Assert.True(!utxo.HasChange);

				var before01Spend = utxo.BlockHash;

				LockTestCoins(tester.Runtime.RPC);
				tester.Runtime.RPC.ImportPrivKey(PrivateKeyOf(key, "0/1"));
				txId = tester.Runtime.RPC.SendToAddress(AddressOf(key, "0/3"), Money.Coins(0.5m));

				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);
				Assert.Equal(1, utxo.Unconfirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/3"), utxo.Unconfirmed.UTXOs[0].KeyPath);
				Assert.Equal(1, utxo.Unconfirmed.SpentOutpoints.Count);

				utxo = tester.Client.Sync(key.Neuter(), utxo.BlockHash, utxo.UnconfirmedHash);
				Assert.True(!utxo.HasChange);
				tester.Runtime.RPC.Generate(1);

				utxo = tester.Client.Sync(key.Neuter(), before01Spend, utxo.UnconfirmedHash);
				Assert.True(!utxo.Unconfirmed.HasChanges);
				Assert.Equal(1, utxo.Confirmed.UTXOs.Count);
				Assert.Equal(new KeyPath("0/3"), utxo.Confirmed.UTXOs[0].KeyPath);
				Assert.Equal(1, utxo.Confirmed.SpentOutpoints.Count);
				Assert.Equal(outpoint01, utxo.Confirmed.SpentOutpoints[0]);
			}
		}

		private void LockTestCoins(RPCClient rpc)
		{
			var outpoints = rpc.ListUnspent().Where(l => l.Address == null).Select(o => o.OutPoint).ToArray();
			rpc.LockUnspent(outpoints);
		}

		private BitcoinSecret PrivateKeyOf(BitcoinExtKey key, string path)
		{
			return new BitcoinSecret(key.ExtKey.Derive(new KeyPath(path)).PrivateKey, key.Network);
		}

		private BitcoinAddress AddressOf(BitcoinExtKey key, string path)
		{
			var address = key.ExtKey.Derive(new KeyPath(path)).Neuter().PubKey.Hash.GetAddress(key.Network);
			var indexes = new KeyPath(path).Indexes;
			indexes[0] = indexes[0] + 2;
			var blinding = key.ExtKey.Derive(new KeyPath(indexes)).Neuter().PubKey;
			return address.AddBlindingKey(blinding);
		}

		[Fact]
		public void CanBroadcast()
		{
			using(var tester = ServerTester.Create())
			{
				Assert.True(tester.Client.Broadcast(_GoodTransaction));
				_GoodTransaction.Inputs[0].PrevOut.N = 999;
				Assert.False(tester.Client.Broadcast(_GoodTransaction));
			}
		}



		Transaction _GoodTransaction = new Transaction("02000000000101f4733a0e18ee9d316e95a63cae43f5e8e76bc89df43a4c8cadbe7ee88eed9c382f00000000fdffffff030b04e1f9a22fa08a51b2c1a1150b2be23deabe0fc0d4ab0f0dad07138b29642d8c08598ee98213cb7669d142371c4cffac1d70972603785c3ece0df8f9b295b1dc42024304028a3d489a0175bbfc8a363c5217929fb2192de55b57b8a586dcb381ebb11976a914a5ea514f7623e63c69ba54c2c0563e10468f0dde88ac0aaf713d5e9a8520acdc468d828db9fa5b47b28ed1ee54b616439f73d2ef46298108b2e21367daec32b1c23ff581bfa0af057b96771a1eb9c5f70ff34dbee09037cc02db2441d5aa3e804fe4260fe9562612f4f82cbaeaeafd1b061632e876538e26431976a914c104ee318aac1d6d93dfde8a53d6270aceea67dc88ac0121667c3dcc51290904a6a9eae27337e6ff5602d0deb5ca501f77be96de63f609010000000000009704000000000043010001051be2c37e28c289141c9dc4ed9d3e0fcdd957fa9e78dee00a1473d0497b029432e0cf780b71c3c15786817f9d1e66cdd2dba23d0aaee029915cb0e21ec91597fd2d0e602c0000000000000001130e1805e28272bf8baf4ccc99eb8c32f31e8d2fb44c616d73921cacafc5bd65da9186b54a2567576bea31a102f9e26721f629ef461899a10488c9beb7ee91ad62050e44789e0e7d8a5ac936860c8483b31edb579b718f3a0208ea5b25dcc64cccb909c6d2418da397dfe1d49b534734029eaa4c4794337519f7dbfb639800f228b5c82cddefcbc9202f7d09874d88bd0042fd122be1e17a717fb884ff82f2a5852c0788651430091c96d347c86f43a00dc2f9fbd72fab7886adc8534e3e855f4eb8f05d883a66f2bac73c3b7ed0de41d72a3c5cb5ca9b8b829145765ea73d1503dc381b9fe74f944ca0195d2e6f19ffce449ec693f4a7054e105b755ebaceed0be25feb20eca3087ae96a4fae2dd5ad3e6299b5446844b334ec73ab57b4545c31d47a25760277ff7ab7d607eef1281ddc53a77d0a0e61e005c8b8d1507a5eeba348e8cb1e1a5e0a30383ce6edf24fa665a3d2ac7be61a8022654ee1bc2b5ad3a291b44d7a3b801b7c1902561595ef4ea2ace96d1f0d1f41f5a6defa79ff87c8c2c67fd1e80497b67105638001081d285f9bbd22c1c96325802298dce711a473ad37eb8bc38fcb2dd78414601cbd3270f13220312ec02ec9b57838197c108171fbdbfae718ded083261e340a589bf2aa94873aa499ada0c87e56a62a59d74dd7ce55c56462704dc9a133f8f314cbe90e987ac120c3cfd08941c5fec0e30cef5307fd1f60b2a9e9aee742adaee4c4ef1b181b0b81f24f74a92ce4eb8d62c269061a4651363a41ab7ad86ea47ed5e7c5fe36ce72d5663e81f0de829bc3b37cd1d4ee407f2c949c17c31bcd1dd0959791c3aa1456a0807b98e97036933e55c093027fa99685263176287a5325734497318358d079d1d2ae5c0752c08c54499a41f56c96bd2c9781df125cd4fc8fabccb4edb47e8205cd5b76ef6de563571412e7ba747db6a85ffb1b687dc3a8c9b724c4ca0450574021c3ce623a7f27702eb24246a25cd2cea09efbb02ae27ca31e9242ef3f754ff1d09451fdf9c7d90d28dfe8649434b5eabedb50185f0533e406e5d2430850f40f225cb375ba135c87c654bac2da0c85c044d6c18e86286fce1f6ab73378248f5e215708a8cb65a15ba5c7592b03be388cc8402c593e0f1b34c6863c79ceb777cce9e2942dcfddc0e2a765efccdd093c0b0d700a440ad48a3dd1f5381b57bd74df47e339aa707f1e81b6b43f21880cdbe0ccd11be52e294860da8f4970fc2a5ff56cd0396a432a486ea658874677c18ac4099a71353486dc689025358a5da57f9dc53c053a72ad62c40ee89665a79595da29091d4afd066ad5dba105f14f2c50074e230479cdb8fc051c1c165d7b66232501b65291db6a1d33b5a4b0331251aef74769c6ba62ea61229760dfa2be39143f579f2ad52258f157a25ae5ee8abf5a32ea873fa4c08be28dda8db45a8d828c675f792b51568bfc3f5cfc5ebf525588160cf5dafcd18ff834e723c58fb54f9c1f6d00b4dcf9c4a9f13714ce0a59b6c227e18f71494409557cfb5e46406b34e116784076ffe525d41322a9eb750bc81628869c419a45e1c5116cedb08031b1baa97212f4ac2a39161a63020df52c955c1d0a6347df82106bc0e50bcbff5b0218a788aed3a0bcb01d4b0ffab5dbf89fe80ea6192e1bbbd70196448c1d14587870e7a39cb3adcc3741e15e78a33386f47a388fc23bae680650da26b28d2af33fef2727689a49dfcecc87983198d9e9ad5169c1ffa20099334a1c253903495098d3875fcef5205092d1a5e6539b7134693d19892aabe73b4d43d3cf17dbb139a877d315926728b133ada62b6aa8972fd7255d6c99c833d4c07f114ba3d3d449763cddf8303dde0089faf5bd0606e18eefa7d38d6ee630f15fcb36831323a0a9eb94455b26c68d6d3b28fa90b48507bb317f633c6fe130f0779dd8ff422b5150d691dc52c2623ace6805b1e9d87443a926a434923474679d8437b43a6163b61d22596d3c526291f9ae372d9a3904040c3933fbc20e4174e943c50f6cd9d72f81b5e9f3847b50ce882d3fa9623fcf0cef68c73552fd4ce88bba8460e669369b7b552643eb5e70e714adc40096b0ad466db4aa3efb45a184214a39e911ff38da2cf01b0968b88ea7e7400da1f30aff127fc083da3e2b853e4a589e6d3ab595ab3f02b30c9eef7c178eb491f75b2536863132316c5f7c3c983fac431b61688812b458d841b75493c419781cb21730b634edbbab07f82b202a71c3594a0b0b5e3f19932995391421ae6b70b5738d9d96af7a071e51a274e3746cf0089f52799749dbae47370bf222295b5ed54e15ed17f059dd4ad7793e03d495694f4239220e174c33a822555cdca4d72e3bd6b63bc7dc6e5586cdc7a9519883796c9396fe9beff635b41f53259612460d92766684566f67a27cd3c3352aa2e5ba36561fd103b6406dacb8e5ef9793917aba6648eb059e1777f559853fca386a1dc6e76a1888623ab59387c813440eb85366cfe3963c95d3ce6f4af5a105b0026a7545f983e5317313f60bfb65f17c5f51b7fcc14dc9192412f7fb927ad2897cd9ef23a31c069acfba77f42d421d1991f7f62655df36a78523312d246a0e40f457c8d42f801726c254f15d05b166f81ae37e2ebb8e2ef5c23a8cff7ea6207e968b1fbf84813ead890b5fad6f41010d7613c853da5f7cba5969446ed4b93efb9e61ebfc09211db16eb0b21ff6d0c1d99c6e24af9f957509da4d0f20f093a151d68487c1b7defb72c474564b2cb2622a2664e6daae1bc7d104d7d49e0bb5bd5d5899f3a3f120e235305cb6319471c22df7b26f74ec1488c51c121fb3831871803e96d52d465e30919d1d435fda0ad76b2d45a7f4ba17f1b88f230a18f89218618fe7103836f2a9c7fe4259c50c23ed8126cf6b6d4fe50dca590796bdde1b2c98c44e36a0bd6f75820fc9e6fc13a89db0f7fc7bedeae3447e68422e874d62b53ebb4ade616788f1cb89e65a997b059cee648e13db85863eba0bc50aa0b28becac03f30c9ba755685a9c75ce5de195fed2471b8593bd9136eb48f09e99f8845935dd8bd8528927138f337a156b346c3150348ff4829ddc27fb62e9678ad09051a42ad5290e6e2a470df1bd8f703778b5413fdcff213e5d7a1355687c132494c0077d8d862459fd9375720a86facc751731a1422f2bb6deaf15a3fa08357b4a8bb7370ba97f56a094b2373a2fea792edaaf5db841bbf7f035761c206dfc1dbd1482a111ec0cc5be24533ede87e29273e94952b2a73ed994a8bd1ef16ce00d00f00b472b5d27d790cf6a704aa1ab265f830052aeddd9a8a7f1fdc09cb15a8639008f854029c089720be983df6d1bc8ad1ea602cdea1d5b8b670ac55bc2d65a8a0d51020160fb83f2496c9a45346957986ff6de43c30603c9b37adb3055c62f6043a9134bebd4d1f8ea1c6172f5c6439be8a2c427c02250f47796989198f5aadceba730b7aafc3d010727eaba2cdb95a36549c8ac0957c7f938668a5393e33f72cecd9f7c48e751b6d556c66f02bbf72d806d37f9d0110ca43c6caffffa80556995ed614957351a5f30ba1e37f18d80dfc29585572ddd2572ecf9d57d41a47171e7cad6db74179ba11ed88ca7a459df458ff119383593d8488ee35aa796c7e52a82e5f7ead18ca96dec75e44d1fe3daabcae1df7af52e60df25dc864fb4b7ae60c3bb969647c739a32b45c003e6f46a8d946a37e7fd9f1ed73d225534e2d10005c1136554ea71d1af578f27460a2be17b868ab1656fda80c9d83418d589979e6fa22ca3ecee0156eb8b675c150310ada8865fcd99870e3f4dcc77630d6424dd24f3568950f614bb07dff0ab031308bcc8f522fa6d45b829621ec1f6307c9a6c3b14e5334de754cda544882b61932f7b43f752b36666f7d008e496610a696d1389257098f46f95f7fecf3c4286b9f75722a4b01f487aafbd092c4382338f29f69cd5baaf32114c4a718030e25d2fd2846914bec947af49e0231f94fa062013d55fa67d4e4abf4c1b78bbf43fa8f8c5267382a1ef3ce4b345baf45ed4771cd9d4208fc375f6c5002a1a5aeb586311eff8c804e27fe0ebb5c1668f579dba8e4f38944179fb1dbca913e7591ad76d312fb1b0905aeff987e9e79918c5a6ee836391ba43f3607b4c609b3e4eee43fd8281016f6bb6ab96084eb712daf79fde88564ea3707cc528ea5c5f7b5ff07a5080b7e9c563eccdd5dd81803f951b77b53a7698c2950237e682fe24d9b3c9ffbebd37c0b281e3284a3fd6aed2ca17d685ce2eb8370e558e674cd01ee15f75b53c9794aa7e821ccbca3eec306021642deadf2a6ae8ae24d44c917f33cd7764dfcf386c537b6cce5bff2e024f939d745c3f7f671bfbf730b60a935f28cc3927b09b37d18d7fa003bd2a4774fc84830c3abc5bc330f2b610f9e9d40313afcf2a65d545a7cb986b628d16a02a5912e6b4fcaf645e3997271f8c266d9887748a115e071061dc176d7f83835d2ba0d1d5399389189ca7d1046517c87b0b33255e6d43eebeee8100ceedd201af07db9766533eaab95ff4912d0a7145ce2505ac8269d3cb7bbc71e191ed68b4fe2457dd3f6d48c7ff3c9bd0b6754b62afae0091a2ab84876bfb09e03737dac3730a34c1c9b0b28bee170259f118fbbb2da90b16ef3f40a83446e147edf193e5038b453894c5f57839750bc5c998041c69f36257152dd600a8b5c58fa0eeb17f5a15266787341c5c076f5ebd8300b42715fa1d36a45dc04cf3f2c1922175cf0d9949ed51df44afb6079041d98f3292bb86ec3975913431572c65ae7a04ef3be552a8863e9c9511379b242ae60374ca17e0543c01a0195c24003ea1036ff9cb2f7f9e9854615606a65f99a16edd36d8cdd75bf493c1aeb86cd96a53ea62b15abab992f4aa0a2e8f479e1ec75bc004d0350151b55615f11e2cff25a3c1d351da207584ecb56b2c765f037a1fd0720fb0c79702872bdb9edb8e36d588e08327f95c6ca8e81f2f9c1d528cf757c7a769ab848f56a8c054a89533f02426605e43dde7768d0056fb9269cb9db4d060327ab7d3f43617931662a219113bf99a1f64ef0da03224a435c0dbb4dccc7f490ad34f06d369c4afbb192e739e43010001ae37d88a114c0fff9ebaf0873ea4a3a916111554be03476f32d67871b1c9520d5882bbee20dbabd87f0fce2ea0db485b4aefcfd1f4dc9ec7cc0294fd0efc5edafd0c0a601f0000000000000001cd4b78c911a48cf835ae1956f1751c6a06158a8548bd33d04a35dd8f5dd788fb2a57771680033364066662672c62c803cd22e9344efbe8b9b4b7ca8960c15eb8e03c2f62ad369fe9b0dbcde349aabf83a5af44d8715c7a9d7b8abe196cf3c94e4d4a0221901e66442273a3b3a261a81df44cdfc2bcdb2d1acdaef92e4b7d46708e3836e71c24e7377e775012ba4597cbeb98f0d468f7925387625e40778195550cd94026a017cd6180278cc2dd07019f148167a8694caf2ddee35cdebde9abb0fea438c2ef50720d2e83424d6737182953ba1e3cb14894b1fad190ef229777fda5bbd455137d2d6b09dea34083e3e97d7fcb82b6c217ddb229ee646e5cfd40d32c1c336fbcaf8806bdb1915a7ad808ff7b3d05a6b1b6064b067fa79a7d17b22af6904514a83445d6d76afc257e96c1103c5dbf18df57a80283102e7ef10d200e6d9e0f66b53bd04c50c3441f8dad7f97c87c3388c4549b7e024e983db19e565dd5c2b34577e5008be4d2b57fc795787e3e4999c0b26abfbdbec373f4efd41bb7771b6673b37f11d5e45fd7c06a973617f3303512d291a1d764bcc28595882cd5d56e688e79d4c8405638b53788788b0c190b62f4d346b97253da80ff96c362a95f72336026ca34aa30bbb971872bff997a639bd1c289a430d0a5545aa3d57ecbd6717527af4df3bb6f8ad101b506112e6ea1c4a76d3c3993384649cd0ac5bed5a4e5d378d93f3979c05885c1c9f38f396e033906e75bf8115e9f61f01654221bca53e6c1cb8e42a97daa3bd9c4dfb3183027eb418567e825aafdbd9e51ea5799b9e2b221e9f8796a22e5b0965b5f545bc10e0121e7ce5341662925e6089ea3bbb34f7e15cdd1b1eaa477cc07f2db560564c74cdb91a7cf1dbab4caf9dd4eb1df8f2d4edeb58f7af85d5d0e0f96acca204ac584e230761e0f53e579f3bf444f4837e488288e14df187a66441b13ca76d6a91aaf152578898fc70c83015c72a5fc19d85320b07f091ec4a537360e18a7a09fc8c6889848e5064195d2d7d95c92b90c2d4f8c581fff06d961c2146e11e81a8ba14b86bb0b1b5e9eac03dcb0d80cf95b829444577549cdbdc3c01eb45bf3a1b87529ee8acc64a488a3aaea1347e6b66587270f1b47c91844d99d0b20243e24aa455281e94b871164b78563fca819aeb57fccaf4d10a145efdae5e8813ed716e2439439f09a4302ec716414e8f6d96aaae7ba5696dfbd1235314a475657c724d8ba5dae6094ff51ce2813e65592ff4825b996d96185c44fbbbb10eab5ba2fdf79f901e942f85c1011efc6988a238f54a20316925148233564a64c25116f970bdf52547879d75dde0cd7e9d2a74f8c785b27b1c5135b3551291197c94f8f8a538cdc5fa768e30d2cc34bf9edfacd396064970345394173550f638f2e0d31ac9e3c0f48f035d465b301f0692a532cb4f5cf18154205d972c5b02b3b3edb760e7775f82905fb2d6381f3302b8944999deeadf7c9952c02ff06d58af6a84dd7b65f34603f8ff133b528bbda5ee5b11c0e23a0691303ac7719b80eb3469d22d214c7facadc7e255f8c07416c586c1f9816b784408d20eb6da79a734d2e9fb411d925088f3c8b924610db53afc6ea2e18cb0c436bc1650e2594420437b9f6cf8e694fe6964bc767116b0fe1071189f08e873643ad79cddcbf78585d7f37cf69cb690bf80765650aab67988c52c0e8fdb595a020e5922935333dfe63e3fc21e39b34743b498bbd31cc1e5f40a6e8bd3542f048c2aa0dd59d18064ce656b83fc3012e105f8fb947fa1326c2998209feb7771e8a138f998771b04ffa2970219810cf491acd35a718333d3e879d38efaf4321e24b4f81c2ddae3dc7ecdefd93c430aed62d62477ab752fdab1b1ef3f6d2fdb4a82878319c5faeb822dea4ed8ea0d5537e8f728f52c283c1cbad1c5228743a2f51d18542f6b71f1d7efe87794666d231d548574bcecaee41c5a68e7981459e5fb18b0f2ecc909b453607db7c388016e5d509373b442e1447e4dcd8079dbdea070324357865e42127787923f4c173703a27b66b5c09fa9b52a367463c4f603077574475bb8145a3bd2e5d9a188d4235d1c8bba0ea3b5b376b28817c19e75545c28bf4775b5ac02e04c61354ca4efa868eb756ee196590ea4d2187cfefb30db4e0fdf520d8b011a33920006739a9cec741f20a6fc666a412a5b86bc85012226b11ce6ad6f44dc3070e11af5442b72bda46035b2e2faffcc8399ed6b3c5fa1831c2e3e16b1866820b9403e647f030202a3ac81520e2abf2fb509fc47528f284107b8297d72dcaddcd87159018ec4e348177327ce16d318aae3fd88d0e01eff2e590b61b7ae6be06de4e538658dd2bd433d5567dfa4df71606e1cb3ad0a44f89f0c2ab16ae5e4a0b43bd213ace30333c7259d5cc9be3a6374ccd02811c8d172204d6b09fab99c5dab71f7fcca3720adda1897222a9f3aa91960617ac335a284fda9e604eb3f0a371a67c8bbc4395ccaef986862e9eff63f7e808796a158a3683611a8f40b450d9963cc25b5a8c83c376dac0dc7be36d500b9f9075d220c3d3e1a7951ec9159618e7228758c69b84854b6b06ce661902d478148dfc1f26a902b14ba45c22ee40cb36bf3dc08801998675c859007f5efce4169d4e057d188b25af6947e30a4c896516e03778acf8a3e8df237e3438b43bf0b7e136799dd9d150656f27846ea97e4876ef3c43063ec374d4186ac3d97d3b0223cd614f4dad74dc4f62669cb2ff5a5376b77b65ce5ade45aa3a819501fd78d7d11074097fb242e06eec80a3d4245b9e5193c1cf4363e60ec8b40ca9e400ace58e30afc485ca7ae7e064299d13d592faa55c465df154139512910ae359d9d101856483a7edbd56ed957955e79868ca4344818f6d05bfe4787dbb36f6861b425c91fb7dab9bd0d5dfa55f279acfd7fee3eac3c68201e97ef7d77eca0a81926b58b2853cec49bd39c5746f1dcddb756bd93918fc69ff97a5bc1a0d87322e81aed13dfe90a59d47d0ec8380c66453046f2f9e5af9e5e144fbdae3090504b8faab0c5ec251e027dbfb76d552bbb89c557334095ad32e3de609e57269ab85f0c93698cd6f2d2f6d0285ff4472e028a31f558675bdb0811781e545cb460d3696c5e49a7da3e38a616eb6edd74e08bb75a70dc90666dd62e24719e35b83c3322798d6c7a4c533fee584a7e3cab0d4ca268450bdf5a8c3bdb1b08a02f121c0696309b2fbce8b1f493764f60db7ea2d150a9c26cb66c535deda182b2095eaa0e9948ed825c7d1baadef27f80cb56997f2a826fb52f3b480e973aba44a8579a9bb99e52791a21ab922577083129e80bbeb4d58c9404997b5797f3e86382fdd1b4ccaa2c78037ca1d7621be6e480851b7ad47c1a18dd7b2d3f40c3486748e885b31c304b5264260dfb4ed1e08165df05742fc675aa8bf271671776b203d27916ab7ef21e7d476bb0a10c9d0478320799ee4d220fda1d4b4b7bcca123accaaf0325d6e0b836d81c5c22e41c3832c7df3a5f40436372ca3e8d6dbd49d8ffb32af286f3e30c9dbc0ff8957946680d06a48790a7b287b65130121d21136df9a917795d68971ec82215ee55553b17383e3fee334e04d24000065000000");
	}
}
