using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

public class PubKeyReuseAnonScoreTests
{
	[Fact]
	public void AddressReusePunishment()
	{
		// If there's reuse in input and output side, then output side didn't gain, nor lose anonymity.
		var km = ServiceFactory.CreateKeyManager();
		var reuse = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100, BitcoinFactory.CreateHdPubKey(km)) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, reuse) });

		// Make the reused key anonymity set something smaller than 109 (which should be the final anonymity set)
		reuse.SetAnonymitySet(30, uint256.One);

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < 30));

		// It should be smaller than 30, because reuse also gets punishment.
		Assert.True(tx.WalletOutputs.First().HdPubKey.AnonymitySet < 30);
	}

	[Fact]
	public void AddressReusePunishmentProcessTwice()
	{
		var km = ServiceFactory.CreateKeyManager();
		var reuse = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100, BitcoinFactory.CreateHdPubKey(km)) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, reuse) });

		// Make the reused key anonymity set something smaller than 109 (which should be the final anonymity set)
		reuse.SetAnonymitySet(30, uint256.One);

		BlockchainAnalyzer.Analyze(tx);
		var inputAnonsets = tx.WalletInputs.Select(x => x.HdPubKey.AnonymitySet).ToArray();
		var outputAnonsets = tx.WalletOutputs.Select(x => x.HdPubKey.AnonymitySet).ToArray();

		BlockchainAnalyzer.Analyze(tx);
		var newInputAnonsets = tx.WalletInputs.Select(x => x.HdPubKey.AnonymitySet).ToArray();
		var newOutputAnonsets = tx.WalletOutputs.Select(x => x.HdPubKey.AnonymitySet).ToArray();

		// Anonsets should not change.
		Assert.Equal(inputAnonsets, newInputAnonsets);
		Assert.Equal(outputAnonsets, newOutputAnonsets);
	}

	[Fact]
	public void SelfSpendReuse()
	{
		var km = ServiceFactory.CreateKeyManager();
		var reuse = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 0,
			Enumerable.Empty<Money>(),
			new[] { (Money.Coins(1.1m), 100, BitcoinFactory.CreateHdPubKey(km)) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, reuse) });

		reuse.SetAnonymitySet(30, uint256.One);

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < 30));

		// It should be smaller than 30, because reuse also gets punishment.
		Assert.True(tx.WalletOutputs.First().HdPubKey.AnonymitySet < 30);
	}

	[Fact]
	public void AddressReuseIrrelevantInNormalSpend()
	{
		// In normal transactions we expose to someone that we own the inputs and the changes
		// So we cannot test address reuse here, because anonsets would be 1 regardless of anything.
		var km = ServiceFactory.CreateKeyManager();
		var key = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 0,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100, key), (Money.Coins(1.2m), 100, key), (Money.Coins(1.3m), 100, key), (Money.Coins(1.4m), 100, key) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, BitcoinFactory.CreateHdPubKey(km)) });

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
		Assert.Equal(1, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputSideAddressReuseHaveNoConsolidationPunishmentInSelfSpend()
	{
		// Consolidation can't hurt any more than reuse already has.
		var km = ServiceFactory.CreateKeyManager();
		var key = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 0,
			Enumerable.Empty<Money>(),
			new[] { (Money.Coins(1.1m), 100, key), (Money.Coins(1.2m), 100, key), (Money.Coins(1.3m), 100, key), (Money.Coins(1.4m), 100, key) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, BitcoinFactory.CreateHdPubKey(km)) });

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(100, x.HdPubKey.AnonymitySet));
		Assert.Equal(100, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputSideAddressReuseHaveNoConsolidationPunishmentInCoinJoin()
	{
		var km = ServiceFactory.CreateKeyManager();
		var key = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100, key), (Money.Coins(1.2m), 100, key), (Money.Coins(1.3m), 100, key), (Money.Coins(1.4m), 100, key) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, BitcoinFactory.CreateHdPubKey(km)) });

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(100, x.HdPubKey.AnonymitySet));
		Assert.Equal(109, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputOutputSideAddress()
	{
		// If there's reuse in input and output side, then output side didn't gain, nor lose anonymity.
		var key = BitcoinFactory.CreateHdPubKey(ServiceFactory.CreateKeyManager());
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100, key) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, key) });

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(100, x.HdPubKey.AnonymitySet));
		Assert.Equal(100, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputOutputSidePreviouslyUsedAddress()
	{
		// If there's reuse in output side, input anonsets should be adjusted down, too.
		var reuse = BitcoinFactory.CreateHdPubKey(ServiceFactory.CreateKeyManager());
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100, BitcoinFactory.CreateHdPubKey(ServiceFactory.CreateKeyManager())) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, reuse) });

		reuse.SetAnonymitySet(30, uint256.One);

		BlockchainAnalyzer.Analyze(tx);

		Assert.True(tx.WalletOutputs.First().HdPubKey.AnonymitySet < 30);
		Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < 30));
	}

	[Fact]
	public void OutputSideAddressReusePunished()
	{
		var km = ServiceFactory.CreateKeyManager();
		var key = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9).Concat(Enumerable.Repeat(Money.Coins(2m), 7)),
			new[] { (Money.Coins(1.1m), 100, BitcoinFactory.CreateHdPubKey(km)) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, key), (Money.Coins(2m), HdPubKey.DefaultHighAnonymitySet, key) });

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(100, x.HdPubKey.AnonymitySet));

		// Normally all levels should have 109 and 106 anonsets, but they're consolidated and punished.
		Assert.All(tx.WalletOutputs.Select(x => x.HdPubKey.AnonymitySet), x => Assert.True(x < 106));
	}

	[Fact]
	public void OutputSideAddressReuseDoesntPunishedMoreThanInheritance()
	{
		// If there's reuse in input and output side, then output side didn't gain, nor lose anonymity.
		var km = ServiceFactory.CreateKeyManager();
		var key = BitcoinFactory.CreateHdPubKey(km);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Enumerable.Repeat(Money.Coins(1m), 9).Concat(Enumerable.Repeat(Money.Coins(2m), 8)).Concat(Enumerable.Repeat(Money.Coins(3m), 7)).Concat(Enumerable.Repeat(Money.Coins(4m), 6)).Concat(Enumerable.Repeat(Money.Coins(5m), 5)).Concat(Enumerable.Repeat(Money.Coins(6m), 4)),
			new[] { (Money.Coins(1.1m), 100, BitcoinFactory.CreateHdPubKey(km)) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet, key), (Money.Coins(2m), HdPubKey.DefaultHighAnonymitySet, key), (Money.Coins(3m), HdPubKey.DefaultHighAnonymitySet, key), (Money.Coins(4m), HdPubKey.DefaultHighAnonymitySet, key), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet, key), (Money.Coins(6m), HdPubKey.DefaultHighAnonymitySet, key) });

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(100, x.HdPubKey.AnonymitySet));

		// 100 is the input anonset, so outputs shouldn't go lower than that.
		Assert.All(tx.WalletOutputs.Select(x => x.HdPubKey.AnonymitySet), x => Assert.True(x >= 100));
	}

	[Fact]
	public void OutputSideAddressReuseBySomeoneElse()
	{
		// If there's reuse in output side by another participant, then we should not gain anonsets by them.
		// https://github.com/WalletWasabi/WalletWasabi/pull/4724/commits/6f5893ca57e35eadb6e20f164bdf0696bb14eea1#r530847724
		var km = ServiceFactory.CreateKeyManager();
		var equalOutputAmount = Money.Coins(1m);
		using var destination = new Key();
		var reusedTxOut = new TxOut(equalOutputAmount, destination);
		var tx = BitcoinFactory.CreateSmartTransaction(
			othersInputCount: 9,
			Common.Repeat(() => new TxOut(equalOutputAmount, new Key()), 7).Concat(new[] { reusedTxOut, reusedTxOut }),
			new[] { (Money.Coins(1.1m), 1, BitcoinFactory.CreateHdPubKey(km)) },
			new[] { (equalOutputAmount, HdPubKey.DefaultHighAnonymitySet, BitcoinFactory.CreateHdPubKey(km)) },
			orderByAmount: false);

		BlockchainAnalyzer.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));

		// Normally it'd be 10, but because of reuse it should be only 8.
		Assert.Equal(8, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void CoinJoinSend()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(2, 2, 40, 0);

		// Make sure that Analyze won't throw in case of no own outputs.
		BlockchainAnalyzer.Analyze(tx);
	}
}
