using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

public static class DenominationBuilder
{
	public static IOrderedEnumerable<Output> CreateDenominations(
		Money minAllowedOutputAmount,
		Money maxAllowedOutputAmount,
		FeeRate feeRate,
		IEnumerable<ScriptType> allowedOutputTypes,
		RandomnessProvider random) =>
		CreateDenominationAmounts(minAllowedOutputAmount, maxAllowedOutputAmount)
			.Select(denomination => Output.FromDenomination(denomination, allowedOutputTypes.RandomElement(random), feeRate))
			.OrderByDescending(x => x.EffectiveAmount);

	public static IReadOnlyList<Money> CreateDenominationAmounts(Money minAllowedOutputAmount, Money maxAllowedOutputAmount)
	{
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
			.Select(denom => Money.Satoshis((ulong)denom))
			.ToArray();
	}
}
