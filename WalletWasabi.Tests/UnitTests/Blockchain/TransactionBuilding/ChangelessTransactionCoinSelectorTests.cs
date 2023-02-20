using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
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
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));
		using Key key = new();

		List<SmartCoin> coins = GenerateDummySmartCoins(key, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);
		Money target = Money.Satoshis(150_000);
		FeeRate feeRate = new(Money.Satoshis(2));
		TxOut txOut = new(target, BitcoinFactory.CreateBitcoinAddress(Network.TestNet, key));

		long[] inputCosts = coins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin, long> inputEffectiveValues = new(coins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));
		StrategyParameters parameters = new(target, inputEffectiveValues.Values.ToArray(), inputCosts);
		MoreSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.True(found);

		long[] solution = selectedCoins!.Select(x => x.Amount.Satoshi).ToArray();
		Assert.Equal(new long[] { 100_000, 50_000, 6_025 }, solution);
		Assert.Equal(Money.Satoshis(6025), solution.Sum() - target); // ~4% more for the privacy.
	}

	[Fact]
	public void Good_LesserSuggestion()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));
		using Key key = new();

		List<SmartCoin> coins = GenerateDummySmartCoins(key, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);
		Money target = Money.Satoshis(65_000);
		FeeRate feeRate = new(Money.Satoshis(2));
		TxOut txOut = new(target, BitcoinFactory.CreateBitcoinAddress(Network.TestNet, key));

		long[] inputCosts = coins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin, long> inputEffectiveValues = new(coins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));
		StrategyParameters parameters = new(target, inputEffectiveValues.Values.ToArray(), inputCosts);
		LessSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.True(found);

		long[] solution = selectedCoins!.Select(x => x.Amount.Satoshi).ToArray();
		Assert.Equal(new long[] { 50_000, 8_192, 6_561 }, solution);
	}

	/// <summary>
	/// Tests that solutions respect <see cref="MoreSelectionStrategy.MaxExtraPayment"/> restriction.
	/// </summary>
	[Fact]
	public void TooExpensiveSolution()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));
		using Key key = new();

		List<SmartCoin> coins = GenerateDummySmartCoins(key, 150_000);
		Money target = Money.Satoshis(100_000);
		FeeRate feeRate = new(Money.Satoshis(2));
		TxOut txOut = new(target, BitcoinFactory.CreateBitcoinAddress(Network.TestNet, key));

		long[] inputCosts = coins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin, long> inputEffectiveValues = new(coins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));
		StrategyParameters parameters = new(target, coins.Select(coin => coin.Amount.Satoshi).ToArray(), inputCosts);
		LessSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
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

		return result.OrderByDescending(x => x.Amount.Satoshi).ToList();
	}
}
