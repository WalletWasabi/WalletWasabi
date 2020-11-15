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
			
			// 10 participants, 1 is you, your anonset is 10.
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

		[Fact]
		public void ChangeOutputInheritence()
		{
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var tx = BitcoinMock.RandomSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(6.2m), 100) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			var active = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
			var change = tx.WalletOutputs.First(x => x.Amount == Money.Coins(5m));

			Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(109, active.HdPubKey.AnonymitySet);
			Assert.Equal(100, change.HdPubKey.AnonymitySet);
		}

		[Fact]
		public void MultiDenomination()
		{
			// Multiple standard denomination outputs results in correct calculation.
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1, 2, 2 };
			var tx = BitcoinMock.RandomSmartTransaction(
				9,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(3.2m), 1) },
				new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(2m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			var level1 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
			var level2 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(2m));

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(4, level1.HdPubKey.AnonymitySet);
			Assert.Equal(3, level2.HdPubKey.AnonymitySet);
		}

		[Fact]
		public void MultiDenominationInheritence()
		{
			// Multiple denominations inherit properly.
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1, 2, 2 };
			var tx = BitcoinMock.RandomSmartTransaction(
				9,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(3.2m), 100) },
				new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(2m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			var level1 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
			var level2 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(2m));

			Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(103, level1.HdPubKey.AnonymitySet);
			Assert.Equal(102, level2.HdPubKey.AnonymitySet);
		}

		[Fact]
		public void SelfAnonsetSanityCheck()
		{
			// If we have multiple same denomination in the same coinjoin, then don't gain anonset on ourselves.
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1 };
			var ownOutputs = new[] { 1, 1 };
			var tx = BitcoinMock.RandomSmartTransaction(
				9,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(3.2m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.All(tx.WalletOutputs, x => Assert.Equal(4, x.HdPubKey.AnonymitySet));
		}

		[Fact]
		public void InputSanityCheck()
		{
			// Anonset can never be larger than the number of inputs.
			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var tx = BitcoinMock.RandomSmartTransaction(2, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(3, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void InputSanityBeforeSelfAnonsetSanityCheck()
		{
			// Input sanity check is executed before self anonset sanity check.

			var analyser = BitcoinMock.RandomBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1 };
			var ownOutputs = new[] { 1, 1 };
			var tx = BitcoinMock.RandomSmartTransaction(
				2,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(3.2m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			// The anonset calculation should be 5, but there's 4 inputs so - 1 = 3.
			// After than - 1 because 2 out of it is ours.
			Assert.All(tx.WalletOutputs, x => Assert.Equal(2, x.HdPubKey.AnonymitySet));
		}

		[Fact]
		public void InputMergePunishment()
		{
			// Input merging results in worse anonset.
		}

		[Fact]
		public void InputMergeProportionalPunishment()
		{
			// Input merging more coins results in worse anonset.
		}

		[Fact]
		public void InputMergePunishmentDependsOnCjSize()
		{
			// Input merging in larger coinjoin results in elss punishmnent.
		}
	}
}
