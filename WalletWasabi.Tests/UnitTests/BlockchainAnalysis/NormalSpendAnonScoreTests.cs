using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

/// <summary>
/// In these tests all the inputs of a transaction are controlled by the user.
/// </summary>
public class NormalSpendAnonScoreTests
{
	[Fact]
	public void OneOwnInOneOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 1, 1, 0);
		var coin = Assert.Single(tx.WalletInputs);
		var key = coin.HdPubKey;
		key.SetAnonymitySet(3, tx.GetHash());

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt our input,
		// so its anonset should become 1.
		Assert.Empty(tx.WalletOutputs);
		Assert.Equal(1, coin.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void ManyOwnInOneOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 1, 3, 0);

		foreach (var coin in tx.WalletInputs)
		{
			coin.HdPubKey.SetAnonymitySet(3, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt our inputs,
		// so its anonset should become 1.
		Assert.Empty(tx.WalletOutputs);
		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void OneOwnInManyOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 3, 1, 0);
		var coin = Assert.Single(tx.WalletInputs);
		var key = coin.HdPubKey;
		key.SetAnonymitySet(3, tx.GetHash());

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt our input,
		// so its anonset should become 1.
		Assert.Empty(tx.WalletOutputs);
		Assert.Equal(1, coin.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void OneOwnInOneOutOneOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 1, 1, 1);

		foreach (var coin in tx.WalletInputs)
		{
			coin.HdPubKey.SetAnonymitySet(3, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt both our input and output,
		// so its anonset should become 1.
		var output = Assert.Single(tx.WalletOutputs);
		Assert.Equal(1, output.HdPubKey.AnonymitySet);
		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void OneOwnInManyOutManyOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 3, 1, 3);

		foreach (var coin in tx.WalletInputs)
		{
			coin.HdPubKey.SetAnonymitySet(3, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt both our input and output,
		// so its anonset should become 1.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void ManyOwnInOneOutOneOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 1, 3, 1);

		foreach (var coin in tx.WalletInputs)
		{
			coin.HdPubKey.SetAnonymitySet(3, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt both our inputs and output,
		// so its anonset should become 1.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void ManyOwnInManyOutManyOwnOut()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(0, 3, 3, 3);

		foreach (var coin in tx.WalletInputs)
		{
			coin.HdPubKey.SetAnonymitySet(3, tx.GetHash());
		}

		BlockchainAnalyzer.Analyze(tx);

		// Since we sent this money to someone we should assume that someone learnt both our inputs and outputs,
		// so its anonset should become 1.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));
	}
}
