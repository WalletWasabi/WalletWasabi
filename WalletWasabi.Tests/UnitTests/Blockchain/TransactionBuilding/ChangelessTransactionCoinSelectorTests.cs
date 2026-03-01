using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionBuilding.BnB;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
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

		KeyManager km = ServiceFactory.CreateKeyManager();
		IEnumerable<SmartCoin> coins = GenerateDummySmartCoins(km, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(150_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
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

		KeyManager km = ServiceFactory.CreateKeyManager();
		List<SmartCoin> coins = GenerateDummySmartCoins(km, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(65_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
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

		KeyManager km = ServiceFactory.CreateKeyManager();
		List<SmartCoin> coins = GenerateDummySmartCoins(km, 150_000);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(100_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<SmartCoin[], long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin).ToArray(), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
		StrategyParameters parameters = new(target, coins.Select(coin => coin.Amount.Satoshi).ToArray(), inputCosts);
		LessSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.False(found);
	}

	/// <summary>
	/// BnB algorithm chooses coins in the way that either all coins with the same scriptPubKey are selected or no coin with that scriptPubKey is selected.
	/// </summary>
	[Fact]
	public async Task BnBRespectScriptPubKeyPrivacyRuleAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		KeyManager km = ServiceFactory.CreateKeyManager();
		HdPubKey constantHdPubKey = BitcoinFactory.CreateHdPubKey(km);
		HdPubKey constantHdPubKey2 = BitcoinFactory.CreateHdPubKey(km);

		// Coins with the same ScriptPubKey should be considered one coin, thus be chosen together.
		List<SmartCoin> availableCoins = new()
		{
			// Group #1.
			BitcoinFactory.CreateSmartCoin(constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey, Money.Satoshis(10000)),

			// Group #2.
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(5000)),
		};

		// First test case where we show that we are not mixing the script pub keys and we spend the address reused coins together.
		{
			Money target = Money.Satoshis(30000);
			TxOut txOut = new(target, scriptPubKey: BitcoinFactory.CreateScript());

			var suggestions = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(availableCoins, FeeRate.Zero, txOut, maxInputCount: 6, testDeadlineCts.Token);

			await foreach (var coins in suggestions)
			{
				// We expect all address-reused coins to be selected together.
				Assert.Single(coins.GroupBy(coin => coin.ScriptPubKey));

				long sumOfCoins = coins.Sum(coin => coin.Amount);

				if (coins.All(coin => coin.ScriptPubKey == constantHdPubKey.P2wpkhScript))  // Less-selection strategy
				{
					Assert.Equal(30_000, sumOfCoins);
					Assert.Equal(3, coins.Count);
				}
				else if (coins.All(coin => coin.ScriptPubKey == constantHdPubKey2.P2wpkhScript))   // More-selection strategy
				{
					Assert.Equal(35_000, sumOfCoins);
					Assert.Equal(4, coins.Count);
				}
				else
				{
					Assert.Fail("Mixed scripts in coin selection!");
				}
			}
		}

		// Second test case where we show that a final selection can contain coins with multiple script pub keys.
		{
			Money target = Money.Satoshis(60_000);
			TxOut txOut = new(target, scriptPubKey: BitcoinFactory.CreateScript());

			var suggestions = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(availableCoins, FeeRate.Zero, txOut, maxInputCount: 10, testDeadlineCts.Token);

			await foreach (var coins in suggestions)
			{
				long sumOfCoins = coins.Sum(coin => coin.Amount);

				if (sumOfCoins == 65_000) // Second case: More-selection strategy.
				{
					Assert.True(coins.All(coin => coin.ScriptPubKey == constantHdPubKey.P2wpkhScript || coin.ScriptPubKey == constantHdPubKey2.P2wpkhScript));
					Assert.Equal(65_000, coins.Sum(coin => coin.Amount));
					Assert.Equal(7, coins.Count);
				}
				else
				{
					Assert.Fail("Unexpected selection!");
				}
			}
		}
	}

	/// <remarks>These smart coins are from an invalid transaction but we are interested only in smart coins' amounts.</remarks>
	private List<SmartCoin> GenerateDummySmartCoins(KeyManager km, params long[] values)
	{
		List<SmartCoin> result = new(capacity: values.Length);

		for (uint i = 0; i < values.Length; i++)
		{
			result.Add(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Satoshis(values[i])));
		}

		return result.OrderByDescending(x => x.Amount.Satoshi).ToList();
	}
}
