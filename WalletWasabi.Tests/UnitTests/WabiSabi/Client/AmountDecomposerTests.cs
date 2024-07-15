using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class AmountDecomposerTests
{
	private static readonly InsecureRandom Random = new(seed: 0);

	[Theory]
	[InlineData(0, 0, 8)]
	[InlineData(0, 0, 1)]
	[InlineData(0, 0, 2)]
	[InlineData(0, 0, 3)]
	[InlineData(0, 1_000, 1)]
	[InlineData(0, 100_000, 2)]
	[InlineData(0, 1_000_000, 3)]
	[InlineData(0, 10_000_000, 8)]
	[InlineData(20, 0, 1)]
	[InlineData(100, 0, 2)]
	[InlineData(500, 0, 3)]
	[InlineData(5000, 0, 8)]
	[InlineData(0, 0, 8, true)]
	[InlineData(0, 0, 1, true)]
	[InlineData(0, 0, 2, true)]
	[InlineData(0, 0, 3, true)]
	[InlineData(0, 1_000, 1, true)]
	[InlineData(0, 100_000, 2, true)]
	[InlineData(0, 1_000_000, 3, true)]
	[InlineData(0, 10_000_000, 8, true)]
	[InlineData(20, 0, 1, true)]
	[InlineData(100, 0, 2, true)]
	[InlineData(500, 0, 3, true)]
	[InlineData(5000, 0, 8, true)]
	public void DecompositionsInvariantTest(decimal feeRateDecimal, long minOutputAmount, int maxAvailableOutputs, bool isTaprootEnabled = false)
	{
		var outputVirtualSize = isTaprootEnabled ? Constants.P2trOutputVirtualSize : Constants.P2wpkhOutputVirtualSize;
		var availableVsize = maxAvailableOutputs * outputVirtualSize;
		var feeRate = new FeeRate(feeRateDecimal);
		var feePerOutput = feeRate.GetFee(outputVirtualSize);
		var registeredCoinEffectiveValues = GenerateRandomCoins().Take(3).Select(c => c.EffectiveValue(feeRate, CoordinationFeeRate.Zero)).ToList();
		var theirCoinEffectiveValues = GenerateRandomCoins().Take(30).Select(c => c.EffectiveValue(feeRate, CoordinationFeeRate.Zero)).ToList();
		var allowedOutputAmountRange = new MoneyRange(Money.Satoshis(minOutputAmount), Money.Satoshis(ProtocolConstants.MaxAmountPerAlice));
		var allowedOutputTypes = isTaprootEnabled ? new List<ScriptType>() { ScriptType.Taproot, ScriptType.P2WPKH } : new List<ScriptType>() { ScriptType.P2WPKH };

		var amountDecomposer = new AmountDecomposer(feeRate, allowedOutputAmountRange.Min, allowedOutputAmountRange.Max, availableVsize, allowedOutputTypes, InsecureRandom.Instance);
		var outputValues = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);

		var totalEffectiveValue = registeredCoinEffectiveValues.Sum(x => x);
		var totalEffectiveCost = outputValues.Sum(x => x.EffectiveCost);

		if (!isTaprootEnabled)
		{
			Assert.InRange(outputValues.Count(), 1, maxAvailableOutputs);
		}
		else
		{
			// The number of outputs cannot be ensure bacause of random scriptype generation. Instead we verify the total.
			Assert.InRange(outputValues.Sum(x => x.ScriptType.EstimateOutputVsize()), 1, availableVsize);
		}

		Assert.True(totalEffectiveValue - totalEffectiveCost - minOutputAmount <= outputValues.Sum(x => x.EffectiveCost));
		Assert.All(outputValues, v => Assert.InRange(v.EffectiveCost.Satoshi, minOutputAmount, totalEffectiveValue));

		var containsTaproot = outputValues.Any(o => o.ScriptType == ScriptType.Taproot);

		if (!isTaprootEnabled)
		{
			Assert.False(containsTaproot);
		}
	}

	private static IEnumerable<Coin> GenerateRandomCoins()
	{
		using var key = new Key();
		var script = key.GetScriptPubKey(ScriptPubKeyType.Segwit);
		while (true)
		{
			var amount = Random.GetInt64(100_000, ProtocolConstants.MaxAmountPerAlice);
			yield return CreateCoin(script, amount);
		}
	}

	private static Coin CreateCoin(Script scriptPubKey, long amount)
	{
		var prevOut = BitcoinFactory.CreateOutPoint();
		var txOut = new TxOut(Money.Satoshis(amount), scriptPubKey);
		return new Coin(prevOut, txOut);
	}

	[Theory]
	[InlineData(2, 7, 2, 3, new long[] { 8, 4, 2 })]
	[InlineData(39, 728551029, 4999, 8, new long[] { 6973569112, 4294967606, 2324523244, 1162261777, 774841288, 536871222, 268435766, 134218038, 86093752, 50000310, 33554742, 20000310, 14349217, 10000310, 5000310, 3188956, 2097462, 1594633, 1063192, 531751, 354604, 262454, 200310, 131382, 100310, 65846, 50310, 39676, 33078, 20310, 16694, 13432, 10310 })]
	public void DecomposeTests(int expectedResultCount, long target, long tolerance, int maxCount, long[] stdDenoms)
	{
		var denoms = stdDenoms.SkipWhile(x => x > target).ToArray();
		var res = Decomposer.Decompose(target, tolerance, maxCount, denoms);

		Assert.True(res.Count() == res.ToHashSet().Count);
		Assert.True(expectedResultCount < 0 || res.Count() == expectedResultCount);
		Assert.All(res, x => Assert.True(x.Sum == Decomposer.ToRealValuesArray(x.Decomposition, x.Count, denoms).Sum()));
		Assert.All(res, x => Assert.True(target - x.Sum < tolerance));
	}
}
