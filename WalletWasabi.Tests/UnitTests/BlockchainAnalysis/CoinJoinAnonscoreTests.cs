using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis
{
	public class CoinJoinAnonscoreTests
	{
		[Fact]
		public void BasicCalculation()
		{
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var tx = BitcoinMock.RandomSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			// 10 participant, 1 is you, your anonset is 10.
			Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void Inheritence()
		{
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var tx = BitcoinMock.RandomSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 100) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			// 10 participants, 1 is you, your anonset is 10 and you inherit 99 anonset,
			// because you don't want to count yourself twice.
			Assert.Equal(109, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void ChangeOutput()
		{
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var tx = BitcoinMock.RandomSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(6.2m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			var active = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
			var change = tx.WalletOutputs.First(x => x.Amount == Money.Coins(5m));

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(10, active.HdPubKey.AnonymitySet);
			Assert.Equal(1, change.HdPubKey.AnonymitySet);
		}
	}
}
