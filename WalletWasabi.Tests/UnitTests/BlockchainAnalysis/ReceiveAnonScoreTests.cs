using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

/// <summary>
/// In these tests no inputs of a transaction are controlled by the user.
/// </summary>
public class ReceiveAnonScoreTests
{
	[Fact]
	public void NormalReceive()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(1, 1, 0, 1);

		BlockchainAnalyzer.Analyze(tx);

		var coin = Assert.Single(tx.WalletOutputs);
		Assert.Equal(1, coin.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void WholeCoinReceive()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(1, 0, 0, 1);

		BlockchainAnalyzer.Analyze(tx);

		var coin = Assert.Single(tx.WalletOutputs);
		Assert.Equal(1, coin.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void CoinjoinReceive()
	{
		var tx = BitcoinFactory.CreateSmartTransaction(10, Enumerable.Repeat(Money.Coins(1m), 9), Enumerable.Empty<(Money, int)>(), new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		BlockchainAnalyzer.Analyze(tx);

		var coin = Assert.Single(tx.WalletOutputs);
		Assert.Equal(1, coin.HdPubKey.AnonymitySet);
	}
}
