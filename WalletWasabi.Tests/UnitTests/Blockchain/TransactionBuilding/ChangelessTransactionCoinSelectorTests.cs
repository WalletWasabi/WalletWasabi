using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
		KeyManager km = ServiceFactory.CreateKeyManager();

		IEnumerable<SmartCoin> coins = GenerateDummySmartCoins(km, 6_025, 6_561, 8_192, 13_122, 50_000, 100_000, 196_939, 524_288);

		var coinsByScript = coins
			.GroupBy(coin => coin.ScriptPubKey.Hash)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(150_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<IEnumerable<SmartCoin>, long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
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
			.GroupBy(coin => coin.ScriptPubKey.Hash)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(65_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coinsByScript.Select(group => group.Sum(coin => feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize()).Satoshi)).ToArray();

		Dictionary<IEnumerable<SmartCoin>, long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
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
			.GroupBy(coin => coin.ScriptPubKey.Hash)
			.OrderByDescending(group => group.Sum(coin => coin.Amount))
			.ToList();

		Money target = Money.Satoshis(100_000);
		FeeRate feeRate = new(Money.Satoshis(2));

		long[] inputCosts = coins.Select(x => feeRate.GetFee(x.ScriptPubKey.EstimateInputVsize()).Satoshi).ToArray();

		Dictionary<IEnumerable<SmartCoin>, long> inputEffectiveValues = new(coinsByScript.ToDictionary(x => x.Select(coin => coin), x => x.Sum(coin => coin.EffectiveValue(feeRate).Satoshi)));
		StrategyParameters parameters = new(target, coins.Select(coin => coin.Amount.Satoshi).ToArray(), inputCosts);
		LessSelectionStrategy strategy = new(parameters);

		bool found = ChangelessTransactionCoinSelector.TryGetCoins(strategy, inputEffectiveValues, out var selectedCoins, testDeadlineCts.Token);
		Assert.False(found);
	}

	[Fact]
	public async void BnBChoosesCoinsWithSameScriptPubKeyAsync()
	{
		using CancellationTokenSource cts = new(TimeSpan.FromSeconds(300));

		KeyManager km = ServiceFactory.CreateKeyManager();
		HdPubKey constantHdPubKey = BitcoinFactory.CreateHdPubKey(km);
		HdPubKey constantHdPubKey2 = BitcoinFactory.CreateHdPubKey(km);

		Money target = Money.Satoshis(30000);

		FeeRate feeRate = new(0m);
		Script destination = BitcoinFactory.CreateScript();

		TxOut txOut = new(target, destination);
		int maxInputCount = 6;

		// Coins with the same ScriptPubKey should be considered one coin, thus be chosen together.
		List<SmartCoin> availableCoins = new()
		{
			BitcoinFactory.CreateSmartCoin(constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(10000)),
			BitcoinFactory.CreateSmartCoin(constantHdPubKey2, Money.Satoshis(5000)),
		};

		var strategies = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(availableCoins, feeRate, txOut, maxInputCount, cts.Token);

		await foreach (var coins in strategies)
		{
			var selectedScripts = coins.GroupBy(coin => coin.ScriptPubKey.Hash);
			Assert.Single(selectedScripts); // Single, so we are sending the address reused coins together and we don't mix them with other scripts.
		}
	}

	/// <remarks>These smart coins are from an invalid transaction but we are interested only in smart coins' amounts.</remarks>
	private List<SmartCoin> GenerateDummySmartCoins(KeyManager km, params long[] values)
	{
		Network network = Network.Main;

		List<SmartCoin> result = new(capacity: values.Length);

		for (uint i = 0; i < values.Length; i++)
		{
			result.Add(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Satoshis(values[i])));
		}

		return result.OrderByDescending(x => x.Amount.Satoshi).ToList();
	}
}
