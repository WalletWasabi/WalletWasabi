using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.BlockchainAnalysis;
using WalletWasabi.Coins;
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
	}
}
