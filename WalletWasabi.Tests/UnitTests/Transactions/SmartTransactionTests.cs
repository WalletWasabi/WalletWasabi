using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class SmartTransactionTests
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
	public void UpdatesWitness()
	{
		// https://github.com/zkSNACKs/WalletWasabi/issues/11119
		var tx = Transaction.Parse("010000000a99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50300000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50a00000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50100000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50800000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50200000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50b00000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50500000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50600000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50000000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50700000000fdffffff018457490200000000160014d5cc08088173e783758a6df6300543e81c04cc6500000000", Network.TestNet);
		var tx2 = Transaction.Parse("0100000000010a99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50300000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50a00000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50100000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50800000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50200000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50b00000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50500000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50600000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50000000000fdffffff99799a67b8d078b4f80f846ad3d18076aff67deff0b7232687ab7031daa502f50700000000fdffffff018457490200000000160014d5cc08088173e783758a6df6300543e81c04cc650247304402202bb069de5b9cfeb9a499841e0d4e1e809c2705910b9263560f72082b577aa5d80220371d97a606acf08f3b40ca7165c3c5c28d4dfdc283ad836638a207bf9a6929ca012103640d3f27cddbebac7a95c0bd3ae5b79789eb3094cc0a1743311c57715bd4392f0247304402203bce78c096d73884f3fbb77106065ec131d13d13cdd0e8547f9e9e6ef41fbb7a02205cb1dc419cec476990d3d2bc1d65898cf7aeac4993460a4c9e7e39b77f81ec08012102f3648e1baa2d3751f5c38450cfbec3c52ef3aff5e8c299d8e722b7c7a947c6750247304402200ae21c381a19a91717bfde9cb58668ce0c8c726d1428ccaba4413137db0cd1a202204698336d77cfe1e2314578bc726c4fea2319ee00f242f583b252f966d253a04301210243d9c95ed1c925d4467157f02132909e801e15fb5e1594dcd73d0452252aace10247304402202ca9c06c9b525fcd6545120a07c148720221593794900c5ec78fba4ca28fb46c02200413e687d2efa56d92ba4c75a0ef31de1f12f4fb9eb691f0c01c2ab8377041ef012103779c2734e11536f9cbf1583858d354cabb4b95ebca05014d101ce7fa4c349edf0247304402203edc440926a5571d3fb29a6190965d8fcc9b817776d81315e67d921f0301d84502206d1dba20353da9f0a61e2d884ab303567aceae4ddcfe7fd60d92231dde4d80dd012102deff1f680e2f02c09c7f78a3cfc9963e042c335a7c91213d5b9f4d5b243207f2024730440220010fa484b4cb63491ba9f655af3976e5ea5babf203bb4f289d518b7ec796cc400220181b84053f99228f719b86449b2a6a8004abef2a179e6a6eb8831864f27de073012103e2dc60fe77ede5c7cf08b2a421528dfcfc14170052c2762a7290f33e37cfac7d0247304402205e68de9ea662b529d8f7a575371c3fd63b3203ae0c56c3480b0ec257fa88ae1a02206822582e5b38210307fba558a9ce1b5ce4c87ac879bfe4fa14c5f993bf09830f012103e7b8cbb1f16e9e07839bc43c5154beb02e4e0125a740038830bba874fe558e460247304402204a6f3d31c4829f0821e3702e3c938b71a0bf7e30b83e96003bae4542ae65bd0902204c2dbf0ee0c46e4f9d77f00fd65a0b0be40a6159b3af89bec066e05b79f4f4d4012103e2a6d202c909c014f9447b08592635d2a496bfcf56f598e9a11a0b8561c5932102473044022023cbfd8eb7688c989f771a3f30de5621907471f42d3b65cbfc57288e9c226b4c02207a087696f13eb6f2ab76898921bfc69a3235c486c9fec8c4bea83e5f22c17323012102bd66f8e83b1914a19f8ab13f2521ede30da86a44164c6617d350873c7419d54f024730440220016b39506020426143a7a22509322b37360813b265c1f7918c26dd580402b5ca02205e81ba6e578d59b0569735a2a29c35ea7f476de8bd8f1872ced63edbf4990d5e01210309cb71a5ca90991346cdd5cdac6df1b63084da66e17aaa98a85d485babc6302c00000000", Network.Main);
		var height = Height.Mempool;

		var smartTx = new SmartTransaction(tx, height);
		var smartTx2 = new SmartTransaction(tx2, height);

		foreach (var input in smartTx.Transaction.Inputs)
		{
			Assert.Equal(WitScript.Empty, input.WitScript);
		}
		foreach (var input in smartTx2.Transaction.Inputs)
		{
			Assert.NotNull(input.WitScript);
			Assert.NotEqual(WitScript.Empty, input.WitScript);
		}

		Assert.True(smartTx.TryUpdate(smartTx2));

		foreach (var input in smartTx.Transaction.Inputs)
		{
			Assert.NotNull(input.WitScript);
			Assert.NotEqual(WitScript.Empty, input.WitScript);
		}
		foreach (var input in smartTx2.Transaction.Inputs)
		{
			Assert.NotNull(input.WitScript);
			Assert.NotEqual(WitScript.Empty, input.WitScript);
		}
	}

	[Fact]
	public void SmartTransactionLineDeserialization()
	{
		// Basic deserialization test.
		var txHash = "dea20cf140bc40d4a6940ac85246989138541e530ed58cbaa010c6b730efd2f6";
		var txHex = "0100000001a67535553fea8a41550e79571359df9e5458b3c2264e37523b0b5d550feecefe0000000000ffffffff017584010000000000160014e1fd78b34c52864ee4a667862f9f9995d850c73100000000";
		var height = "Unknown";
		var blockHash = "";
		var blockIndex = "0";
		var label = "foo";
		var unixSeconds = "1567084917";
		var isReplacement = "False";
		var isCpfp = "False";
		var isSpeedup = "False";
		var isCancellation = "False";
		SmartTransaction stx;
		foreach (var net in new[] { Network.Main, Network.TestNet, Network.RegTest })
		{
			foreach (var inp in new[]
			{
					// Legacy input.
					$"{txHash}:{txHex}:{height}:{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}",

					// Normal input.
					$"{txHash}:{txHex}:{height}:{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}:{isCpfp}:{isSpeedup}:{isCancellation}",

					// Whitespaces.
					$"     {txHash} :   {txHex}  :  {height}  :  {blockHash} :  {blockIndex}  : {label}    : {unixSeconds}     :    {isReplacement} :  {isCpfp}: {isSpeedup} :   {isCancellation}  ",

					// Don't fail on more inputs.
					$"{txHash}:{txHex}:{height}:{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}::",
					$"{txHash}:{txHex}:{height}:{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}:bar:buz",
					$"{txHash}:{txHex}:{height}:{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}:{isCpfp}:{isSpeedup}:{isCancellation}:bar:buz",

					// Can leave out some inputs.
					$":{txHex}:{height}:{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}",
					$"{txHash}:{txHex}::{blockHash}:{blockIndex}:{label}:{unixSeconds}:{isReplacement}",
					$"{txHash}:{txHex}:{height}::{blockIndex}:{label}:{unixSeconds}:{isReplacement}",
					$"{txHash}:{txHex}:{height}:{blockHash}::{label}:{unixSeconds}:{isReplacement}:{isCpfp}:{isSpeedup}:{isCancellation}"
				})
			{
				stx = SmartTransaction.FromLine(inp, net);

				if (inp == $"{txHex}")
				{
					Assert.Equal(txHash, stx.GetHash().ToString());
					Assert.Equal(txHex, stx.Transaction.ToHex());
					Assert.Equal(Height.Unknown, stx.Height);
					Assert.Null(stx.BlockHash);
					Assert.Equal(0, stx.BlockIndex);
					Assert.True(stx.Labels.IsEmpty);
					Assert.Equal(stx.FirstSeen.UtcDateTime, DateTime.UtcNow, TimeSpan.FromSeconds(1));
					Assert.False(stx.IsReplacement);
					Assert.False(stx.IsSpeedup);
					Assert.False(stx.IsCancellation);
				}
				else
				{
					Assert.Equal(txHash, stx.GetHash().ToString());
					Assert.Equal(txHex, stx.Transaction.ToHex());
					Assert.Equal(height, stx.Height.ToString());
					Assert.Equal(blockHash, Guard.Correct(stx.BlockHash?.ToString()));
					Assert.Equal(blockIndex, stx.BlockIndex.ToString());
					Assert.Equal(label, stx.Labels);
					Assert.Equal(unixSeconds, stx.FirstSeen.ToUnixTimeSeconds().ToString());
					Assert.Equal(isReplacement, stx.IsReplacement.ToString());
					Assert.Equal(isSpeedup, stx.IsSpeedup.ToString());
					Assert.Equal(isCancellation, stx.IsCancellation.ToString());
				}
			}
		}
	}

	[Fact]
	public void SmartTransactionVirtualMembersEquals()
	{
		SmartTransaction st = BitcoinFactory.CreateSmartTransaction(0, 1, 1, 1);

		var originalSentBytes = st.WalletInputs.First().HdPubKey.PubKey.ToBytes();
		var virtualSentBytes = st.WalletVirtualInputs.First().Id;
		var originalSentCoin = st.WalletInputs.First().Coin;
		var virtualSentCoin = st.WalletVirtualInputs.First().Coins.First().Coin;
		Assert.Equal(originalSentBytes, virtualSentBytes);
		Assert.Equal(originalSentCoin, virtualSentCoin);

		var outputBytes = st.WalletOutputs.First().HdPubKey.PubKey.ToBytes();
		var outputVirtualBytes = st.WalletVirtualOutputs.First().Id;
		var originalOutputAmount = st.WalletOutputs.First().Coin.Amount;
		var virtualOutputAmount = st.WalletVirtualOutputs.First().Amount;
		Assert.Equal(outputBytes, outputVirtualBytes);
		Assert.Equal(originalOutputAmount, virtualOutputAmount);

		var foreingOutputsAmount = st.ForeignOutputs.First().ToCoin().Amount;
		var foreingVirtualOutputsAmount = st.ForeignVirtualOutputs.First().Amount;
		Assert.Equal(foreingOutputsAmount, foreingVirtualOutputsAmount);
	}

	[Fact]
	public void SmartTransactionVirtualForeignOutputMerge()
	{
		var km = ServiceFactory.CreateKeyManager("");
		var network = km.GetNetwork();
		HdPubKey hdPubKey = BitcoinFactory.CreateHdPubKey(km);

		Script samePubScript1 = hdPubKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network).ScriptPubKey;
		Script samePubScript2 = hdPubKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ScriptPubKey;

		Transaction t = Transaction.Create(network);

		TxOut txout = new(Money.Coins(1), samePubScript1);
		TxOut txout2 = new(Money.Coins(1), samePubScript2);

		t.Outputs.Add(txout);
		t.Outputs.Add(txout2);
		SmartTransaction st1 = new(t, 0);

		Assert.Single(st1.ForeignVirtualOutputs);
		Assert.Equal(2, st1.ForeignVirtualOutputs.First().OutPoints.Count);

		Transaction t2 = Transaction.Create(network);

		TxOut txout3 = new(Money.Coins(1), samePubScript1);
		TxOut txout4 = new(Money.Coins(1), samePubScript1);

		t2.Outputs.Add(txout3);
		t2.Outputs.Add(txout4);
		SmartTransaction st2 = new(t2, 0);

		Assert.Single(st2.ForeignVirtualOutputs);
		Assert.Equal(2, st2.ForeignVirtualOutputs.First().OutPoints.Count);
	}

	[Fact]
	public void SmartTransactionVirtualWalletInputMerge()
	{
		var km = ServiceFactory.CreateKeyManager("");
		var network = km.GetNetwork();
		HdPubKey hdPubKey = BitcoinFactory.CreateHdPubKey(km);

		Transaction t = Transaction.Create(network);

		SmartTransaction st1 = new(t, 0);

		var sc = BitcoinFactory.CreateSmartCoin(hdPubKey, Money.Coins(1));
		var sc2 = BitcoinFactory.CreateSmartCoin(hdPubKey, Money.Coins(2));

		st1.TryAddWalletInput(sc);
		st1.TryAddWalletInput(sc2);

		Assert.Single(st1.WalletVirtualInputs);
		Assert.Equal(2, st1.WalletVirtualInputs.First().Coins.Count);
	}

	[Fact]
	public void SmartTransactionVirtualWalletOutputMerge()
	{
		var km = ServiceFactory.CreateKeyManager("");
		var network = km.GetNetwork();
		HdPubKey hdPubKey = BitcoinFactory.CreateHdPubKey(km);

		Transaction t = Transaction.Create(network);

		var sc = BitcoinFactory.CreateSmartCoin(t, hdPubKey, Money.Coins(1));
		var sc2 = BitcoinFactory.CreateSmartCoin(t, hdPubKey, Money.Coins(2));
		Assert.NotEqual(sc, sc2);

		var st1 = sc.Transaction;
		st1.TryAddWalletOutput(sc);
		st1.TryAddWalletOutput(sc2);

		Assert.Single(st1.WalletVirtualOutputs);
		Assert.Equal(Money.Coins(3), st1.WalletVirtualOutputs.First().Amount);
		Assert.Equal(2, st1.WalletVirtualOutputs.First().Coins.Count);
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

		var blockHashes = new List<uint256?>
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

		var firstSeens = new List<DateTimeOffset>
		{
			DateTimeOffset.UtcNow,
			DateTimeOffset.Now,
			DateTimeOffset.MaxValue,
			DateTimeOffset.MinValue,
			DateTimeOffset.UnixEpoch
		};

		var booleans = new List<bool>
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
			yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, labels: label), defaultNetwork };
		}

		foreach (var firstSeen in firstSeens)
		{
			yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, firstSeen: firstSeen), defaultNetwork };
		}

		foreach (var isReplacement in booleans)
		{
			yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, isReplacement: isReplacement), defaultNetwork };
		}

		foreach (var isSpeedup in booleans)
		{
			yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, isSpeedup: isSpeedup), defaultNetwork };
		}

		foreach (var isCancellation in booleans)
		{
			yield return new object[] { new SmartTransaction(defaultTx, defaultHeight, isCancellation: isCancellation), defaultNetwork };
		}
	}
}
