using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

/// <summary>
/// In these tests all the inputs and outputs of a transaction are controlled by the user.
/// </summary>
public class SelfSpendAnonScoreTests
{
	[Fact]
	public void OneOwnInOneOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 0, 1, 1);
		var coin = Assert.Single(tx.WalletInputs);
		var key = coin.HdPubKey;
		key.SetAnonymitySet(3, tx.GetHash());

		BlockchainAnalyzer.Analyze(tx);

		// Anonset of the input shall be retained.
		// Although the tx has more than one interpretation
		// blockchain anal usually just assumes it's a self spend.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(3, x.HdPubKey.AnonymitySet));
		Assert.All(tx.WalletInputs, x => Assert.Equal(3, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void OneOwnInManyOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 0, 1, 3);
		var coin = Assert.Single(tx.WalletInputs);
		var key = coin.HdPubKey;
		key.SetAnonymitySet(3, tx.GetHash());

		BlockchainAnalyzer.Analyze(tx);

		// Anonset of the input shall be retained.
		// Although the tx has many interpretations we shall not guess which one
		// a blockchain analyzer would go with, therefore outputs shall not gain anonsets
		// as we're conservatively estimating.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(3, x.HdPubKey.AnonymitySet));
		Assert.All(tx.WalletInputs, x => Assert.Equal(3, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void ManyOwnInOneOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 0, 3, 1);
		var smallestAnonset = 3;

		tx.WalletInputs.First().HdPubKey.SetAnonymitySet(smallestAnonset, uint256.One);
		foreach (var coin in tx.WalletInputs.Skip(1))
		{
			coin.HdPubKey.SetAnonymitySet(100, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Anonset of the input shall be worsened because of input merging.
		Assert.All(tx.WalletOutputs, x => Assert.True(x.HdPubKey.AnonymitySet < smallestAnonset));
		Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < smallestAnonset));
	}

	[Fact]
	public void ManyOwnInManyOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 0, 3, 3);
		var smallestAnonset = 3;

		tx.WalletInputs.First().HdPubKey.SetAnonymitySet(smallestAnonset, uint256.One);
		foreach (var coin in tx.WalletInputs.Skip(1))
		{
			coin.HdPubKey.SetAnonymitySet(100, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Anonset of the input shall be worsened because of input merging.
		// Although the tx has many interpretations we shall not guess which one
		// a blockchain analyzer would go with, therefore outputs shall not gain anonsets
		// as we're conservatively estimating.
		Assert.All(tx.WalletOutputs, x => Assert.True(x.HdPubKey.AnonymitySet < smallestAnonset));
		Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < smallestAnonset));
	}
}
