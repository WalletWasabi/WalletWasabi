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
	public class CoinJoinAnonScoreTests
	{
		[Fact]
		public void BasicCalculation()
		{
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

			// 10 participants, 1 is you, your anonset is 10.
			Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void DoubleProcessing()
		{
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });
			analyser.Analyze(tx);
			analyser.Analyze(tx);
			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

			// 10 participants, 1 is you, your anonset is 10.
			Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void OtherWalletChangesThings()
		{
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 8), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });
			var sc = tx.WalletOutputs.First();
			tx.WalletOutputs.Remove(sc);
			analyser.Analyze(tx);
			tx.WalletOutputs.Add(sc);
			analyser.Analyze(tx);
			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

			// 10 participants, 2 is you, your anonset is 10/2 = 5.
			Assert.Equal(5, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(5, tx.WalletOutputs.Skip(1).First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void Inheritance()
		{
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 100) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);

			// 10 participants, 1 is you, your anonset is 10 and you inherit 99 anonset,
			// because you don't want to count yourself twice.
			Assert.Equal(109, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void ChangeOutput()
		{
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(6.2m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			var active = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
			var change = tx.WalletOutputs.First(x => x.Amount == Money.Coins(5m));

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(10, active.HdPubKey.AnonymitySet);
			Assert.Equal(1, change.HdPubKey.AnonymitySet);
		}

		[Fact]
		public void ChangeOutputInheritance()
		{
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(6.2m), 100) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet) });

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
			// Multiple standard denomination outputs should be accounted separately.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1, 2, 2 };
			var tx = BitcoinFactory.CreateSmartTransaction(
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
		public void MultiDenominationInheritance()
		{
			// Multiple denominations inherit properly.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1, 2, 2 };
			var tx = BitcoinFactory.CreateSmartTransaction(
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
			// If we have multiple same denomination in the same CoinJoin, then our anonset would be total coins/our coins.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1 };
			var ownOutputs = new[] { 1, 1 };
			var tx = BitcoinFactory.CreateSmartTransaction(
				9,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(3.2m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.All(tx.WalletOutputs, x => Assert.Equal(5 / 2, x.HdPubKey.AnonymitySet));
		}

		[Fact]
		public void InputSanityCheck()
		{
			// Anonset can never be larger than the number of inputs.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(2, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
			Assert.Equal(3, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void InputSanityBeforeSelfAnonsetSanityCheck()
		{
			// Input sanity check is executed before self anonset sanity check.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var othersOutputs = new[] { 1, 1, 1 };
			var ownOutputs = new[] { 1, 1 };
			var tx = BitcoinFactory.CreateSmartTransaction(
				2,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(3.2m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

			analyser.Analyze(tx);

			Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

			// The anonset calculation naively would be 5,
			// but there's only 3 inputs so that limits our anonset to 3.
			// After that we should get 3/2 because 2 out of 3 is ours.
			// Finally we don't mess around with decimal precisions, so
			// conservatively 3/2 = 1.
			Assert.All(tx.WalletOutputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
		}

		[Fact]
		public void InputMergePunishment()
		{
			// Input merging results in worse inherited anonset.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(
				9,
				Enumerable.Repeat(Money.Coins(1m), 9),
				new[] { (Money.Coins(1.1m), 100), (Money.Coins(1.2m), 200), (Money.Coins(1.3m), 300), (Money.Coins(1.4m), 400) },
				new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < 100));

			// 10 participants, 1 is you, your added anonset would be 10.
			// Smallest input anonset is 100 so your anonset would be 109 normally, but 4 inputs are merged so it should be worse.
			Assert.True(tx.WalletOutputs.First().HdPubKey.AnonymitySet < 109);
		}

		[Fact]
		public void InputMergePunishmentNoInheritance()
		{
			// Input merging results in worse inherited anonset, but does not punish gains from output indistinguishability.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();
			var tx = BitcoinFactory.CreateSmartTransaction(
				9,
				Enumerable.Repeat(Money.Coins(1m), 9),
				new[] { (Money.Coins(1.1m), 1), (Money.Coins(1.2m), 1), (Money.Coins(1.3m), 1), (Money.Coins(1.4m), 1) },
				new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

			analyser.Analyze(tx);

			Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));

			// 10 participants, 1 is you, your anonset would be 10 normally and now too:
			Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void InputMergeProportionalPunishment()
		{
			// Input merging more coins results in worse anonset.
			// In this test tx1 consolidates less inputs than tx2.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();

			var othersInputCount = 9;
			var othersOutputs = Enumerable.Repeat(Money.Coins(1m), 9);
			var ownInputs1 = new[] { (Money.Coins(1.1m), 100), (Money.Coins(1.2m), 200), (Money.Coins(1.3m), 300), (Money.Coins(1.4m), 400) };
			var ownInputs2 = ownInputs1.Concat(new[] { (Money.Coins(1.5m), 100) });
			var ownOutputs = new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) };

			var tx1 = BitcoinFactory.CreateSmartTransaction(
				othersInputCount,
				othersOutputs,
				ownInputs1,
				ownOutputs);

			var tx2 = BitcoinFactory.CreateSmartTransaction(
				othersInputCount,
				othersOutputs,
				ownInputs2,
				ownOutputs);

			analyser.Analyze(tx1);
			analyser.Analyze(tx2);

			Assert.All(tx1.WalletInputs, x => Assert.All(tx2.WalletInputs, y => Assert.True(x.HdPubKey.AnonymitySet > y.HdPubKey.AnonymitySet)));
			Assert.True(tx1.WalletOutputs.First().HdPubKey.AnonymitySet > tx2.WalletOutputs.First().HdPubKey.AnonymitySet);
		}

		[Fact]
		public void InputMergePunishmentDependsOnCjSize()
		{
			// Input merging in larger coinjoin results in less punishmnent.
			var analyser = ServiceFactory.CreateBlockchainAnalyzer();

			var othersInputCount1 = 10;
			var othersInputCount2 = 9;
			var othersOutputs = Enumerable.Repeat(Money.Coins(1m), 9);
			var ownInputs = new[] { (Money.Coins(1.1m), 100), (Money.Coins(1.2m), 200), (Money.Coins(1.3m), 300), (Money.Coins(1.4m), 400) };
			var ownOutputs = new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) };

			var tx1 = BitcoinFactory.CreateSmartTransaction(
				othersInputCount1,
				othersOutputs,
				ownInputs,
				ownOutputs);

			var tx2 = BitcoinFactory.CreateSmartTransaction(
				othersInputCount2,
				othersOutputs,
				ownInputs,
				ownOutputs);

			analyser.Analyze(tx1);
			analyser.Analyze(tx2);

			Assert.All(tx1.WalletInputs, x => Assert.All(tx2.WalletInputs, y => Assert.True(x.HdPubKey.AnonymitySet > y.HdPubKey.AnonymitySet)));
			Assert.True(tx1.WalletOutputs.First().HdPubKey.AnonymitySet > tx2.WalletOutputs.First().HdPubKey.AnonymitySet);
		}
	}
}
