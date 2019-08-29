using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;
using Xunit;

namespace WalletWasabi.Tests
{
	public class ModelTests
	{
		[Fact]
		public void SmartTransactionEquality()
		{
			var hex = "0100000000010176f521a178a4b394b647169a89b29bb0d95b6bce192fd686d533eb4ea98464a20000000000ffffffff02ed212d020000000016001442da650f25abde0fef57badd745df7346e3e7d46fbda6400000000001976a914bd2e4029ce7d6eca7d2c3779e8eac36c952afee488ac02483045022100ea43ccf95e1ac4e8b305c53761da7139dbf6ff164e137a6ce9c09e15f316c22902203957818bc505bbafc181052943d7ab1f3ae82c094bf749813e8f59108c6c268a012102e59e61f20c7789aa73faf5a92dc8c0424e538c635c55d64326d95059f0f8284200000000";
			var tx = Transaction.Parse(hex, Network.TestNet);
			var tx2 = Transaction.Parse(hex, Network.Main);
			var tx3 = Transaction.Parse(hex, Network.RegTest);
			var height = Height.Mempool;

			var smartTx = new SmartTransaction(tx, height);
			var differentSmartTx = new SmartTransaction(Network.TestNet.Consensus.ConsensusFactory.CreateTransaction(), height);
			var s1 = new SmartTransaction(tx, Height.Unknown);
			var s2 = new SmartTransaction(tx, new Height(2));
			var s3 = new SmartTransaction(tx2, height);
			var s4 = new SmartTransaction(tx3, height);

			Assert.Equal(s1, smartTx);
			Assert.Equal(s2, smartTx);
			Assert.Equal(s3, smartTx);
			Assert.Equal(s4, smartTx);
			Assert.NotEqual(smartTx, differentSmartTx);
		}

		[Fact]
		public void SmartCoinEquality()
		{
			var tx = Transaction.Parse("0100000000010176f521a178a4b394b647169a89b29bb0d95b6bce192fd686d533eb4ea98464a20000000000ffffffff02ed212d020000000016001442da650f25abde0fef57badd745df7346e3e7d46fbda6400000000001976a914bd2e4029ce7d6eca7d2c3779e8eac36c952afee488ac02483045022100ea43ccf95e1ac4e8b305c53761da7139dbf6ff164e137a6ce9c09e15f316c22902203957818bc505bbafc181052943d7ab1f3ae82c094bf749813e8f59108c6c268a012102e59e61f20c7789aa73faf5a92dc8c0424e538c635c55d64326d95059f0f8284200000000", Network.TestNet);
			var txId = tx.GetHash();
			var index = 0U;
			var output = tx.Outputs[0];
			var scriptPubKey = output.ScriptPubKey;
			var amount = output.Value;
			var spentOutputs = tx.Inputs.ToTxoRefs().ToArray();

			var height = Height.Mempool;
			var label = "foo";

			var coin = new SmartCoin(txId, index, scriptPubKey, amount, spentOutputs, height, tx.RBF, tx.GetAnonymitySet(index), isLikelyCoinJoinOutput: false, label, txId);
			// If the txId or the index differs, equality should think it's a different coin.
			var differentCoin = new SmartCoin(txId, index + 1, scriptPubKey, amount, spentOutputs, height, tx.RBF, tx.GetAnonymitySet(index + 1), isLikelyCoinJoinOutput: false, label, txId);
			var differentOutput = tx.Outputs[1];
			var differentSpentOutputs = new[]
			{
				new TxoRef(txId, 0)
			};
			// If the txId and the index is the same, equality should think it's the same coin.
			var sameCoin = new SmartCoin(txId, index, differentOutput.ScriptPubKey, differentOutput.Value, differentSpentOutputs, Height.Unknown, tx.RBF, tx.GetAnonymitySet(index), isLikelyCoinJoinOutput: false, "boo", null);

			Assert.Equal(coin, sameCoin);
			Assert.NotEqual(coin, differentCoin);
		}

		[Fact]
		public void SmartTransactionJsonSerialization()
		{
			var tx = Transaction.Parse("02000000040aa8d0af84518df6e3a60c5bb19d9c3fcc3dc6e26b2f2449e8d7bf8d3fe84b87010000006a473044022018dfe9216c1209dd6c2b6c1607dbac4e499c1fce4878bc7d5d83fccbf3e24c9402202cac351c9c6a2b5eef338cbf0ec000d8de1c05e96a904cbba2b9e6ffc2d4e19501210364cc39da1091b1a9c12ec905a14a9e8478f951f7a1accdabeb40180533f2eaa5feffffff112c07d0f5e0617d720534f0b2b84dc0d5b7314b358c3ab338823b9e5bfbddf5010000006b483045022100ec155e7141e74661ee511ae980150a6c89261f31070999858738369afc28f2b6022006230d2aa24fac110b74ef15b84371486cf76c539b335a253c14462447912a300121020c2f41390f031d471b22abdb856e6cdbe0f4d74e72c197469bfd54e5a08f7e67feffffff38e799b8f6cf04fd021a9b135cdcd347da7aac4fd8bb8d0da9316a9fb228bb6e000000006b483045022100fc1944544a3f96edd8c8a9795c691e2725612b5ab2e1c999be11a2a4e3f841f1022077b2e088877829edeada0c707a9bb577aa79f26dafacba3d1d2d047f52524296012102e6015963dff9826836400cf8f45597c0705757d5dcdc6bf734f661c7dab89e69feffffff64c3f0377e86625123f2f1ee229319ed238e8ca8b7dda5bc080a2c5ecb984629000000006a47304402204233a90d6296182914424fd2901e16e6f5b13b451b67b0eec25a5eaacc5033c902203d8a13ef0b494c12009663475458e51da6bd55cc67688264230ece81d3eeca24012102f806d7152da2b52c1d9ad928e4a6253ccba080a5b9ab9efdd80e37274ac67f9bfeffffff0290406900000000001976a91491ac4e49b66f845180d98d8f8be6121588be6e3b88ac52371600000000001976a9142f44ed6749e8c84fd476e4440741f7e6f55542fa88acadd30700", Network.TestNet);
			var tx2 = Transaction.Parse("02000000040aa8d0af84518df6e3a60c5bb19d9c3fcc3dc6e26b2f2449e8d7bf8d3fe84b87010000006a473044022018dfe9216c1209dd6c2b6c1607dbac4e499c1fce4878bc7d5d83fccbf3e24c9402202cac351c9c6a2b5eef338cbf0ec000d8de1c05e96a904cbba2b9e6ffc2d4e19501210364cc39da1091b1a9c12ec905a14a9e8478f951f7a1accdabeb40180533f2eaa5feffffff112c07d0f5e0617d720534f0b2b84dc0d5b7314b358c3ab338823b9e5bfbddf5010000006b483045022100ec155e7141e74661ee511ae980150a6c89261f31070999858738369afc28f2b6022006230d2aa24fac110b74ef15b84371486cf76c539b335a253c14462447912a300121020c2f41390f031d471b22abdb856e6cdbe0f4d74e72c197469bfd54e5a08f7e67feffffff38e799b8f6cf04fd021a9b135cdcd347da7aac4fd8bb8d0da9316a9fb228bb6e000000006b483045022100fc1944544a3f96edd8c8a9795c691e2725612b5ab2e1c999be11a2a4e3f841f1022077b2e088877829edeada0c707a9bb577aa79f26dafacba3d1d2d047f52524296012102e6015963dff9826836400cf8f45597c0705757d5dcdc6bf734f661c7dab89e69feffffff64c3f0377e86625123f2f1ee229319ed238e8ca8b7dda5bc080a2c5ecb984629000000006a47304402204233a90d6296182914424fd2901e16e6f5b13b451b67b0eec25a5eaacc5033c902203d8a13ef0b494c12009663475458e51da6bd55cc67688264230ece81d3eeca24012102f806d7152da2b52c1d9ad928e4a6253ccba080a5b9ab9efdd80e37274ac67f9bfeffffff0290406900000000001976a91491ac4e49b66f845180d98d8f8be6121588be6e3b88ac52371600000000001976a9142f44ed6749e8c84fd476e4440741f7e6f55542fa88acadd30700", Network.Main);
			var tx3 = Transaction.Parse("02000000040aa8d0af84518df6e3a60c5bb19d9c3fcc3dc6e26b2f2449e8d7bf8d3fe84b87010000006a473044022018dfe9216c1209dd6c2b6c1607dbac4e499c1fce4878bc7d5d83fccbf3e24c9402202cac351c9c6a2b5eef338cbf0ec000d8de1c05e96a904cbba2b9e6ffc2d4e19501210364cc39da1091b1a9c12ec905a14a9e8478f951f7a1accdabeb40180533f2eaa5feffffff112c07d0f5e0617d720534f0b2b84dc0d5b7314b358c3ab338823b9e5bfbddf5010000006b483045022100ec155e7141e74661ee511ae980150a6c89261f31070999858738369afc28f2b6022006230d2aa24fac110b74ef15b84371486cf76c539b335a253c14462447912a300121020c2f41390f031d471b22abdb856e6cdbe0f4d74e72c197469bfd54e5a08f7e67feffffff38e799b8f6cf04fd021a9b135cdcd347da7aac4fd8bb8d0da9316a9fb228bb6e000000006b483045022100fc1944544a3f96edd8c8a9795c691e2725612b5ab2e1c999be11a2a4e3f841f1022077b2e088877829edeada0c707a9bb577aa79f26dafacba3d1d2d047f52524296012102e6015963dff9826836400cf8f45597c0705757d5dcdc6bf734f661c7dab89e69feffffff64c3f0377e86625123f2f1ee229319ed238e8ca8b7dda5bc080a2c5ecb984629000000006a47304402204233a90d6296182914424fd2901e16e6f5b13b451b67b0eec25a5eaacc5033c902203d8a13ef0b494c12009663475458e51da6bd55cc67688264230ece81d3eeca24012102f806d7152da2b52c1d9ad928e4a6253ccba080a5b9ab9efdd80e37274ac67f9bfeffffff0290406900000000001976a91491ac4e49b66f845180d98d8f8be6121588be6e3b88ac52371600000000001976a9142f44ed6749e8c84fd476e4440741f7e6f55542fa88acadd30700", Network.RegTest);
			var height = Height.Mempool;

			string label = "foo";
			var smartTx = new SmartTransaction(tx, height, label: label);
			var smartTx2 = new SmartTransaction(tx2, height);
			var smartTx3 = new SmartTransaction(tx3, height);

			var serialized = JsonConvert.SerializeObject(smartTx);
			var deserialized = JsonConvert.DeserializeObject<SmartTransaction>(serialized);
			Assert.Equal(label, smartTx.Label);

			var serialized2 = JsonConvert.SerializeObject(smartTx2);
			var deserialized2 = JsonConvert.DeserializeObject<SmartTransaction>(serialized2);

			var serialized3 = JsonConvert.SerializeObject(smartTx3);
			var deserialized3 = JsonConvert.DeserializeObject<SmartTransaction>(serialized3);

			Assert.Equal(smartTx, deserialized);
			Assert.Equal(smartTx.Height, deserialized.Height);
			Assert.Equal(deserialized, deserialized2);
			Assert.Equal(deserialized, deserialized3);
			Assert.True(smartTx.Transaction == deserialized3);
			Assert.True(smartTx.Equals(deserialized2.Transaction));
			object sto = deserialized;
			Assert.True(smartTx.Equals(sto));
			Assert.True(smartTx.Equals(deserialized.Transaction));
			// ToDo: Assert.True(smartTx.Equals(to));

			var serializedWithoutLabel = "{\"Transaction\":\"02000000040aa8d0af84518df6e3a60c5bb19d9c3fcc3dc6e26b2f2449e8d7bf8d3fe84b87010000006a473044022018dfe9216c1209dd6c2b6c1607dbac4e499c1fce4878bc7d5d83fccbf3e24c9402202cac351c9c6a2b5eef338cbf0ec000d8de1c05e96a904cbba2b9e6ffc2d4e19501210364cc39da1091b1a9c12ec905a14a9e8478f951f7a1accdabeb40180533f2eaa5feffffff112c07d0f5e0617d720534f0b2b84dc0d5b7314b358c3ab338823b9e5bfbddf5010000006b483045022100ec155e7141e74661ee511ae980150a6c89261f31070999858738369afc28f2b6022006230d2aa24fac110b74ef15b84371486cf76c539b335a253c14462447912a300121020c2f41390f031d471b22abdb856e6cdbe0f4d74e72c197469bfd54e5a08f7e67feffffff38e799b8f6cf04fd021a9b135cdcd347da7aac4fd8bb8d0da9316a9fb228bb6e000000006b483045022100fc1944544a3f96edd8c8a9795c691e2725612b5ab2e1c999be11a2a4e3f841f1022077b2e088877829edeada0c707a9bb577aa79f26dafacba3d1d2d047f52524296012102e6015963dff9826836400cf8f45597c0705757d5dcdc6bf734f661c7dab89e69feffffff64c3f0377e86625123f2f1ee229319ed238e8ca8b7dda5bc080a2c5ecb984629000000006a47304402204233a90d6296182914424fd2901e16e6f5b13b451b67b0eec25a5eaacc5033c902203d8a13ef0b494c12009663475458e51da6bd55cc67688264230ece81d3eeca24012102f806d7152da2b52c1d9ad928e4a6253ccba080a5b9ab9efdd80e37274ac67f9bfeffffff0290406900000000001976a91491ac4e49b66f845180d98d8f8be6121588be6e3b88ac52371600000000001976a9142f44ed6749e8c84fd476e4440741f7e6f55542fa88acadd30700\",\"Height\":\"2147483646\"}";
			var deserializedWithoutLabel = JsonConvert.DeserializeObject<SmartTransaction>(serializedWithoutLabel);
			Assert.Empty(deserializedWithoutLabel.Label);
		}

		[Theory]
		[MemberData(nameof(GetSmartTransactionCombinations))]
		public void SmartTransactionLineSerialization(SmartTransaction stx, Network network)
		{
			var line = stx.ToLine();
			var sameStx = SmartTransaction.FromLine(line, network);
			Assert.Equal(stx, sameStx);
			Assert.Equal(stx.BlockHash, sameStx.BlockHash);
			Assert.Equal(stx.BlockIndex, sameStx.BlockIndex);
			Assert.Equal(stx.Confirmed, sameStx.Confirmed);
			Assert.Equal(stx.FirstSeenIfMempoolTime.HasValue, sameStx.FirstSeenIfMempoolTime.HasValue);
			if (stx.FirstSeenIfMempoolTime.HasValue)
			{
				Assert.Equal(stx.FirstSeenIfMempoolTime.Value.UtcDateTime, sameStx.FirstSeenIfMempoolTime.Value.UtcDateTime, TimeSpan.FromSeconds(1));
			}
			Assert.Equal(stx.Height, sameStx.Height);
			Assert.Equal(stx.IsRBF, sameStx.IsRBF);
			Assert.Equal(stx.IsReplacement, sameStx.IsReplacement);
			Assert.Equal(stx.Label, sameStx.Label);
			Assert.Equal(stx.Transaction.GetHash(), sameStx.Transaction.GetHash());
		}

		public static IEnumerable<object[]> GetSmartTransactionCombinations()
		{
			var networks = new List<Network>
			{
				Network.Main,
				Network.TestNet,
				Network.RegTest
			};
			var defaultNetwork = Network.Main;

			var txHexes = new List<string>
			{
				"02000000040aa8d0af84518df6e3a60c5bb19d9c3fcc3dc6e26b2f2449e8d7bf8d3fe84b87010000006a473044022018dfe9216c1209dd6c2b6c1607dbac4e499c1fce4878bc7d5d83fccbf3e24c9402202cac351c9c6a2b5eef338cbf0ec000d8de1c05e96a904cbba2b9e6ffc2d4e19501210364cc39da1091b1a9c12ec905a14a9e8478f951f7a1accdabeb40180533f2eaa5feffffff112c07d0f5e0617d720534f0b2b84dc0d5b7314b358c3ab338823b9e5bfbddf5010000006b483045022100ec155e7141e74661ee511ae980150a6c89261f31070999858738369afc28f2b6022006230d2aa24fac110b74ef15b84371486cf76c539b335a253c14462447912a300121020c2f41390f031d471b22abdb856e6cdbe0f4d74e72c197469bfd54e5a08f7e67feffffff38e799b8f6cf04fd021a9b135cdcd347da7aac4fd8bb8d0da9316a9fb228bb6e000000006b483045022100fc1944544a3f96edd8c8a9795c691e2725612b5ab2e1c999be11a2a4e3f841f1022077b2e088877829edeada0c707a9bb577aa79f26dafacba3d1d2d047f52524296012102e6015963dff9826836400cf8f45597c0705757d5dcdc6bf734f661c7dab89e69feffffff64c3f0377e86625123f2f1ee229319ed238e8ca8b7dda5bc080a2c5ecb984629000000006a47304402204233a90d6296182914424fd2901e16e6f5b13b451b67b0eec25a5eaacc5033c902203d8a13ef0b494c12009663475458e51da6bd55cc67688264230ece81d3eeca24012102f806d7152da2b52c1d9ad928e4a6253ccba080a5b9ab9efdd80e37274ac67f9bfeffffff0290406900000000001976a91491ac4e49b66f845180d98d8f8be6121588be6e3b88ac52371600000000001976a9142f44ed6749e8c84fd476e4440741f7e6f55542fa88acadd30700",
				"0200000001268171371edff285e937adeea4b37b78000c0566cbb3ad64641713ca42171bf6000000006a473044022070b2245123e6bf474d60c5b50c043d4c691a5d2435f09a34a7662a9dc251790a022001329ca9dacf280bdf30740ec0390422422c81cb45839457aeb76fc12edd95b3012102657d118d3357b8e0f4c2cd46db7b39f6d9c38d9a70abcb9b2de5dc8dbfe4ce31feffffff02d3dff505000000001976a914d0c59903c5bac2868760e90fd521a4665aa7652088ac00e1f5050000000017a9143545e6e33b832c47050f24d3eeb93c9c03948bc787b32e1300",
				"0100000002d8c8df6a6fdd2addaf589a83d860f18b44872d13ee6ec3526b2b470d42a96d4d000000008b483045022100b31557e47191936cb14e013fb421b1860b5e4fd5d2bc5ec1938f4ffb1651dc8902202661c2920771fd29dd91cd4100cefb971269836da4914d970d333861819265ba014104c54f8ea9507f31a05ae325616e3024bd9878cb0a5dff780444002d731577be4e2e69c663ff2da922902a4454841aa1754c1b6292ad7d317150308d8cce0ad7abffffffff2ab3fa4f68a512266134085d3260b94d3b6cfd351450cff021c045a69ba120b2000000008b4830450220230110bc99ef311f1f8bda9d0d968bfe5dfa4af171adbef9ef71678d658823bf022100f956d4fcfa0995a578d84e7e913f9bb1cf5b5be1440bcede07bce9cd5b38115d014104c6ec27cffce0823c3fecb162dbd576c88dd7cda0b7b32b0961188a392b488c94ca174d833ee6a9b71c0996620ae71e799fc7c77901db147fa7d97732e49c8226ffffffff02c0175302000000001976a914a3d89c53bb956f08917b44d113c6b2bcbe0c29b788acc01c3d09000000001976a91408338e1d5e26db3fce21b011795b1c3c8a5a5d0788ac00000000"
			};
			var defaultTx = Transaction.Parse(txHexes.First(), defaultNetwork);

			var heights = new List<Height>
			{
				Height.Unknown,
				Height.Mempool,
				new Height(0),
				new Height(100),
				new Height(int.MaxValue)
			};
			var defaultHeight = new Height(0);

			var blockHashes = new List<uint256>
			{
				null,
				uint256.Parse("000000000000000000093e2e41b170cd9e10ed8a0469c9719abd227d5226672f")
			};

			var blockIndexes = new List<int>
			{
				0,
				1,
				100,
				int.MaxValue
			};

			var labels = new List<string>
			{
				"",
				" ",
				"foo",
				"foo, bar",
				"               :foo:bar:buz: ",
				"~!@#$%^&*()"
			};

			var firstSeens = new List<DateTimeOffset?>
			{
				null,
				DateTimeOffset.UtcNow,
				DateTimeOffset.Now,
				DateTimeOffset.MaxValue,
				DateTimeOffset.MinValue,
				DateTimeOffset.UnixEpoch
			};

			var isReplacements = new List<bool>
			{
				false,
				true
			};

			foreach (var network in networks)
			{
				foreach (var txHex in txHexes)
				{
					var tx = Transaction.Parse(txHex, network);
					foreach (var height in heights)
					{
						yield return new object[] { new SmartTransaction(tx, height), network };
					}
				}
			}

			foreach (var blockHash in blockHashes)
			{
				yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, blockHash), defaultNetwork };
			}

			foreach (var blockIndex in blockIndexes)
			{
				yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, blockIndex: blockIndex), defaultNetwork };
			}
			foreach (var label in labels)
			{
				yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, label: label), defaultNetwork };
			}

			foreach (var firstSeen in firstSeens)
			{
				yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, firstSeenIfMempoolTime: firstSeen), defaultNetwork };
			}

			foreach (var isReplacement in isReplacements)
			{
				yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, isReplacement: isReplacement), defaultNetwork };
			}
		}

		[Fact]
		public void UtxoRefereeSerialization()
		{
			var record = BannedUtxoRecord.FromString("2018-11-23 15-23-14:1:44:2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492:True:195");

			Assert.Equal(new DateTimeOffset(2018, 11, 23, 15, 23, 14, TimeSpan.Zero), record.TimeOfBan);
			Assert.Equal(1, record.Severity);
			Assert.Equal(44u, record.Utxo.N);
			Assert.Equal(new uint256("2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492"), record.Utxo.Hash);
			Assert.True(record.IsNoted);
			Assert.Equal(195, record.BannedForRound);

			DateTimeOffset dateTime = DateTimeOffset.UtcNow;
			DateTimeOffset now = new DateTimeOffset(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
			var record2Init = new BannedUtxoRecord(record.Utxo, 3, now, false, 99);
			string record2Line = record2Init.ToString();
			var record2 = BannedUtxoRecord.FromString(record2Line);

			Assert.Equal(now, record2.TimeOfBan);
			Assert.Equal(3, record2.Severity);
			Assert.Equal(44u, record2.Utxo.N);
			Assert.Equal(new uint256("2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492"), record2.Utxo.Hash);
			Assert.False(record2.IsNoted);
			Assert.Equal(99, record2.BannedForRound);
		}

		[Fact]
		public void SmartCoinSerialization()
		{
			var tx = Transaction.Parse("02000000040aa8d0af84518df6e3a60c5bb19d9c3fcc3dc6e26b2f2449e8d7bf8d3fe84b87010000006a473044022018dfe9216c1209dd6c2b6c1607dbac4e499c1fce4878bc7d5d83fccbf3e24c9402202cac351c9c6a2b5eef338cbf0ec000d8de1c05e96a904cbba2b9e6ffc2d4e19501210364cc39da1091b1a9c12ec905a14a9e8478f951f7a1accdabeb40180533f2eaa5feffffff112c07d0f5e0617d720534f0b2b84dc0d5b7314b358c3ab338823b9e5bfbddf5010000006b483045022100ec155e7141e74661ee511ae980150a6c89261f31070999858738369afc28f2b6022006230d2aa24fac110b74ef15b84371486cf76c539b335a253c14462447912a300121020c2f41390f031d471b22abdb856e6cdbe0f4d74e72c197469bfd54e5a08f7e67feffffff38e799b8f6cf04fd021a9b135cdcd347da7aac4fd8bb8d0da9316a9fb228bb6e000000006b483045022100fc1944544a3f96edd8c8a9795c691e2725612b5ab2e1c999be11a2a4e3f841f1022077b2e088877829edeada0c707a9bb577aa79f26dafacba3d1d2d047f52524296012102e6015963dff9826836400cf8f45597c0705757d5dcdc6bf734f661c7dab89e69feffffff64c3f0377e86625123f2f1ee229319ed238e8ca8b7dda5bc080a2c5ecb984629000000006a47304402204233a90d6296182914424fd2901e16e6f5b13b451b67b0eec25a5eaacc5033c902203d8a13ef0b494c12009663475458e51da6bd55cc67688264230ece81d3eeca24012102f806d7152da2b52c1d9ad928e4a6253ccba080a5b9ab9efdd80e37274ac67f9bfeffffff0290406900000000001976a91491ac4e49b66f845180d98d8f8be6121588be6e3b88ac52371600000000001976a9142f44ed6749e8c84fd476e4440741f7e6f55542fa88acadd30700", Network.TestNet);
			var txId = tx.GetHash();
			var index = 0U;
			var output = tx.Outputs[0];
			var scriptPubKey = output.ScriptPubKey;
			var amount = output.Value;
			var spentOutputs = tx.Inputs.ToTxoRefs().ToArray();
			var height = Height.Mempool;
			var label = "foo";
			var bannedUntil = DateTimeOffset.UtcNow;

			var coin = new SmartCoin(txId, index, scriptPubKey, amount, spentOutputs, height, tx.RBF, tx.GetAnonymitySet(index), isLikelyCoinJoinOutput: false, label, txId);
			coin.BannedUntilUtc = bannedUntil;

			var serialized = JsonConvert.SerializeObject(coin);
			var deserialized = JsonConvert.DeserializeObject<SmartCoin>(serialized);

			Assert.Equal(coin, deserialized);
			Assert.Equal(coin.Height, deserialized.Height);
			Assert.Equal(coin.Amount, deserialized.Amount);
			Assert.Equal(coin.Index, deserialized.Index);
			Assert.Equal(coin.Unavailable, deserialized.Unavailable);
			Assert.Equal(coin.Label, deserialized.Label);
			Assert.Equal(coin.ScriptPubKey, deserialized.ScriptPubKey);
			Assert.Equal(coin.SpenderTransactionId, deserialized.SpenderTransactionId);
			Assert.Equal(coin.TransactionId, deserialized.TransactionId);
			Assert.Equal(coin.SpentOutputs.Length, deserialized.SpentOutputs.Length);
			Assert.Equal(coin.BannedUntilUtc, deserialized.BannedUntilUtc);
			for (int i = 0; i < coin.SpentOutputs.Length; i++)
			{
				Assert.Equal(coin.SpentOutputs[0], deserialized.SpentOutputs[0]);
			}
		}

		[Fact]
		public void AllFeeEstimateSerialization()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 102 },
				{ 3, 20 },
				{ 19, 1 }
			};
			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations);
			var serialized = JsonConvert.SerializeObject(allFee);
			var deserialized = JsonConvert.DeserializeObject<AllFeeEstimate>(serialized);

			Assert.Equal(estimations[2], deserialized.Estimations[2]);
			Assert.Equal(estimations[3], deserialized.Estimations[3]);
			Assert.Equal(estimations[19], deserialized.Estimations[19]);
			Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized.Type);
		}

		[Fact]
		public void InputsResponseSerialization()
		{
			var resp = new InputsResponse
			{
				UniqueId = Guid.NewGuid(),
				RoundId = 1,
			};
			var serialized = JsonConvert.SerializeObject(resp);
			var deserialized = JsonConvert.DeserializeObject<InputsResponse>(serialized);

			Assert.Equal(resp.RoundId, deserialized.RoundId);
			Assert.Equal(resp.UniqueId, deserialized.UniqueId);
		}

		[Fact]
		public void ObservableConcurrentHashSetTest()
		{
			Set_CollectionChangedLock = new object();
			Set_CollectionChangedInvokeCount = 0;
			var set = new ObservableConcurrentHashSet<int>();

			set.CollectionChanged += Set_CollectionChanged;
			try
			{
				// CollectionChanged fire 1
				set.TryAdd(1);
				Assert.Contains(1, set);
				Assert.Single(set);

				// CollectionChanged do not fire
				set.TryAdd(1);
				Assert.Contains(1, set);
				Assert.Single(set);

				// CollectionChanged do not fire
				set.TryRemove(2);
				Assert.Single(set);

				// CollectionChanged fire 2
				set.TryAdd(2);
				Assert.Contains(2, set);
				Assert.Equal(2, set.Count);

				// CollectionChanged fire 3
				set.TryRemove(2);
				Assert.Contains(1, set);
				Assert.DoesNotContain(2, set);
				Assert.Single(set);

				// CollectionChanged fire 4
				set.TryAdd(3);
				Assert.Contains(1, set);
				Assert.Contains(3, set);
				Assert.Equal(2, set.Count);

				// CollectionChanged fire 5
				set.Clear();
				Assert.NotNull(set);
				Assert.Empty(set);
			}
			finally
			{
				set.CollectionChanged -= Set_CollectionChanged;
			}
		}

		private int Set_CollectionChangedInvokeCount { get; set; }
		private object Set_CollectionChangedLock { get; set; }

		private void Set_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			lock (Set_CollectionChangedLock)
			{
				Set_CollectionChangedInvokeCount++;

				switch (Set_CollectionChangedInvokeCount)
				{
					case 1:
						{
							Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
							Assert.Single(e.NewItems);
							Assert.Null(e.OldItems);
							Assert.Equal(1, e.NewItems[0]);
							break;
						}
					case 2:
						{
							Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
							Assert.Single(e.NewItems);
							Assert.Null(e.OldItems);
							Assert.Equal(2, e.NewItems[0]);
							break;
						}
					case 3:
						{
							Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
							Assert.Null(e.NewItems);
							Assert.Single(e.OldItems);
							Assert.Equal(2, e.OldItems[0]);
							break;
						}
					case 4:
						{
							Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
							Assert.Single(e.NewItems);
							Assert.Null(e.OldItems);
							Assert.Equal(3, e.NewItems[0]);
							break;
						}
					case 5:
						{
							Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
							Assert.Null(e.NewItems);
							Assert.Null(e.OldItems); // "Reset action must be initialized with no changed items."
							break;
						}
					default:
						throw new NotSupportedException();
				}
			}
		}
	}
}
