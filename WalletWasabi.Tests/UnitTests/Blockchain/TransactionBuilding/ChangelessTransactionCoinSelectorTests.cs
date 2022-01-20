using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBuilding;

/// <summary>
/// Tests for <see cref="ChangelessTransactionCoinSelector"/> class.
/// </summary>
public class ChangelessTransactionCoinSelectorTests
{
	/// <summary>
	/// Tests that we select coins so that user can agree with paying more money for having a payment transaction with no change output.
	/// </summary>
	[Fact]
	public void GoodSuggestion()
	{
		using Key key = new();

		List<SmartCoin> coins = GenerateDummySmartCoins(key, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);
		var target = Money.Satoshis(150_000);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(coins, new FeeRate(satoshiPerByte: 4), target, out IEnumerable<SmartCoin>? selectedCoins);
		Assert.True(found);

		long[] solution = selectedCoins!.Select(x => x.Amount.Satoshi).ToArray();
		Assert.Equal(new long[] { 100_000, 50_000, 6_025 }, solution);
		Assert.Equal(Money.Satoshis(6025), solution.Sum() - target); // ~4% more for the privacy.
	}

	/// <summary>
	/// Tests that solutions respect <see cref="ChangelessTransactionCoinSelector.MaxExtraFee"/> restriction.
	/// </summary>
	[Fact]
	public void TooExpensiveSolution()
	{
		using Key key = new();

		List<SmartCoin> coins = GenerateDummySmartCoins(key, 150_000);
		var target = Money.Satoshis(100_000);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(coins, new FeeRate(satoshiPerByte: 4), target, out IEnumerable<SmartCoin>? selectedCoins);
		Assert.False(found);
	}

	/// <remarks>These smart coins are from an invalid transaction but we are interested only in smart coins' amounts.</remarks>
	private List<SmartCoin> GenerateDummySmartCoins(Key key, params long[] values)
	{
		Network network = Network.Main;
		Transaction tx = Transaction.Create(network);
		tx.Inputs.Add(TxIn.CreateCoinbase(200));

		foreach (long satoshis in values)
		{
			tx.Outputs.Add(Money.Satoshis(satoshis), BitcoinFactory.CreateBitcoinAddress(network, key));
		}

		SmartTransaction stx = new(tx, Height.Mempool);
		List<SmartCoin> result = new(capacity: values.Length);

		for (uint i = 0; i < values.Length; i++)
		{
			HdPubKey hdPubKey = new(key.PubKey, new KeyPath($"m/84h/0h/0h/1/{i}"), SmartLabel.Empty, KeyState.Clean);
			result.Add(new SmartCoin(stx, i, hdPubKey));
		}

		return result;
	}
}
