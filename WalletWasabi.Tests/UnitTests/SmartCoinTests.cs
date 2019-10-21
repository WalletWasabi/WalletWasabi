using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class SmartCoinTests
	{
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
			var label = new SmartLabel("foo");

			var coin = new SmartCoin(txId, index, scriptPubKey, amount, spentOutputs, height, tx.RBF, tx.GetAnonymitySet(index), isLikelyCoinJoinOutput: false, label, txId);
			// If the txId or the index differ, equality should think it's a different coin.
			var differentCoin = new SmartCoin(txId, index + 1, scriptPubKey, amount, spentOutputs, height, tx.RBF, tx.GetAnonymitySet(index + 1), isLikelyCoinJoinOutput: false, label, txId);
			var differentOutput = tx.Outputs[1];
			var differentSpentOutputs = new[]
			{
				new TxoRef(txId, 0)
			};

			// If the txId and the index are the same, equality should think it's the same coin.
			var sameCoin = new SmartCoin(txId, index, differentOutput.ScriptPubKey, differentOutput.Value, differentSpentOutputs, Height.Unknown, tx.RBF, tx.GetAnonymitySet(index), isLikelyCoinJoinOutput: false, new SmartLabel("boo"), null);

			Assert.Equal(coin, sameCoin);
			Assert.NotEqual(coin, differentCoin);
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
			var label = new SmartLabel("foo");
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
	}
}
