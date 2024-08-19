using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

public static class DenominationBuilder
{
	public static IOrderedEnumerable<Output> CreateDenominations(
		Money minAllowedOutputAmount,
		Money maxAllowedOutputAmount,
		FeeRate feeRate,
		IEnumerable<ScriptType> allowedOutputTypes,
		WasabiRandom random)
	{
		Output CreateDenom(decimal sats) =>
			Output.FromDenomination(Money.Satoshis((ulong)sats), allowedOutputTypes.RandomElement(random), feeRate);

		IEnumerable<decimal> Times(int times, IEnumerable<decimal> values) =>
			values
				.Select(value => times * value)
				.SkipWhile(denom => denom < minAllowedOutputAmount.Satoshi)
				.TakeWhile(denom => denom <= maxAllowedOutputAmount.Satoshi);

		IEnumerable<decimal> PowersOf(double baseValue) =>
			Enumerable.Range(0, short.MaxValue)
				.Select(i => (decimal)Math.Pow(baseValue, i));

		return Times(1, PowersOf(2))
			.Concat(Times(1, PowersOf(3)))
			.Concat(Times(2, PowersOf(3)))
			.Concat(Times(1, PowersOf(10)))
			.Concat(Times(2, PowersOf(10)))
			.Concat(Times(5, PowersOf(10)))
			.ToHashSet()
			.Select(CreateDenom)
			.OrderByDescending(x => x.EffectiveAmount);
	}
}
