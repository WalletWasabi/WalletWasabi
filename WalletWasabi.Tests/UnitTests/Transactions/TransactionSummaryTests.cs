using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class TransactionSummaryTests
{
	/// <summary>The UI reads a summary while the next block is processed. Before, this threw "Collection was modified".</summary>
	[Fact]
	public void SummaryDoesNotChangeWhenTransactionGetsMoreCoins()
	{
		var km = ServiceFactory.CreateKeyManager();
		var coins = Enumerable.Range(0, 3).Select(_ => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), 1m)).ToArray();
		var tx = Transaction.Create(Network.Main);

		foreach (var coin in coins)
		{
			tx.Inputs.Add(coin.Outpoint);
		}

		tx.Outputs.Add(Money.Coins(2.9m), new Key());
		var stx = new SmartTransaction(tx, Height.Mempool);
		Assert.True(stx.TryAddWalletInput(coins[0]));

		var summary = new TransactionSummary(stx, Money.Zero, null, km);

		foreach (var _ in summary.WalletInputs)
		{
			Assert.True(stx.TryAddWalletInput(coins[1]));
		}

		Assert.Single(summary.WalletInputs);
		Assert.Equal(2, summary.ForeignInputs().Count);
		Assert.Equal(2, stx.WalletInputs.Count);
	}
}
