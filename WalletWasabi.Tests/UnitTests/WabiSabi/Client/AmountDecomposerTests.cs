using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class AmountDecomposerTests
{
	private static readonly Random Random = new(1234567);

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

		var amountDecomposer = new AmountDecomposer(feeRate, allowedOutputAmountRange, availableVsize, isTaprootEnabled, Random);
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

	[Fact]
	public void StandardDenominations()
	{
		var amountDecomposer = new AmountDecomposer(FeeRate.Zero, new MoneyRange(Money.Coins(0.00005m), Money.Coins(43_000m)), 0, false, Random);

		var expectedDenominations = new[]
		{
			5000L, 6561L, 8192L, 10000L, 13122L, 16384L, 19683L, 20000L, 32768L, 39366L, 50000L, 59049L,
			65536L, 100000L, 118098L, 131072L, 177147L, 200000L, 262144L, 354294L, 500000L, 524288L,
			531441L, 1000000L, 1048576L, 1062882L, 1594323L, 2000000L, 2097152L, 3188646L, 4194304L,
			4782969L, 5000000L, 8388608L, 9565938L, 10000000L, 14348907L, 16777216L, 20000000L, 28697814L,
			33554432L, 43046721L, 50000000L, 67108864L, 86093442L, 100000000L, 129140163L, 134217728L,
			200000000L, 258280326L, 268435456L, 387420489L, 500000000L, 536870912L, 774840978L,
			1000000000L, 1073741824L, 1162261467L, 2000000000L, 2147483648L, 2324522934L, 3486784401L,
			4294967296L, 5000000000L, 6973568802L, 8589934592L, 10000000000L, 10460353203L, 17179869184L,
			20000000000L, 20920706406L, 31381059609L, 34359738368L, 50000000000L, 62762119218L,
			68719476736L, 94143178827L, 100000000000L, 137438953472L, 188286357654L, 200000000000L,
			274877906944L, 282429536481L, 500000000000L, 549755813888L, 564859072962L, 847288609443L,
			1000000000000L, 1099511627776L, 1694577218886L, 2000000000000L, 2199023255552L, 2541865828329L
		};
		Assert.Equal(expectedDenominations, amountDecomposer.Denominations.Select(x => x.Amount.Satoshi).Reverse());
	}

	private static IEnumerable<Coin> GenerateRandomCoins()
	{
		using var key = new Key();
		var script = key.GetScriptPubKey(ScriptPubKeyType.Segwit);
		while (true)
		{
			var amount = Random.NextInt64(100_000, ProtocolConstants.MaxAmountPerAlice);
			yield return CreateCoin(script, amount);
		}
	}

	private static Coin CreateCoin(Script scriptPubKey, long amount)
	{
		var prevOut = BitcoinFactory.CreateOutPoint();
		var txOut = new TxOut(Money.Satoshis(amount), scriptPubKey);
		return new Coin(prevOut, txOut);
	}
}
