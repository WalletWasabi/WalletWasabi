using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	public static class StandardDenomination
	{
		public static readonly ImmutableArray<Money> Values = Generate().ToImmutableArray();

		private static IEnumerable<long> Multiple(IEnumerable<long> coefficients, IEnumerable<long> values) =>
			values
			.SelectMany(c => coefficients, (v, c) => c * v)
			.TakeWhile(x => x <= ProtocolConstants.MaxAmountPerAlice);

		private static IEnumerable<long> PowersOf(int b) =>
			Enumerable
			.Range(0, int.MaxValue)
			.Select(x => (long)Math.Pow(b, x))
			.TakeWhile(x => x <= ProtocolConstants.MaxAmountPerAlice);

		private static IEnumerable<Money> Generate()
		{
			var ternary = Multiple(new long[] { 1, 2 }, PowersOf(3));
			var preferredValueSeries = Multiple(new long[] { 1, 2, 5 }, PowersOf(10));

			return PowersOf(2)
				.Union(ternary)
				.Union(preferredValueSeries)
				.OrderBy(v => v)
				.Select(Money.Satoshis);
		}
	}
}
