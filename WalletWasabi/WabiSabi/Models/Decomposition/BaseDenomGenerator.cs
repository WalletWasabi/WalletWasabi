using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	public static class BaseDenominationGenerator
	{
		private static Money MaxSats = Money.Satoshis(2099999997690000);

		private static IEnumerable<Money> Multiple(IEnumerable<long> coefficients, IEnumerable<long> values) =>
			values.SelectMany(v => coefficients.Select(c => Money.Satoshis(c * v))).Where(x => x <= MaxSats);

		public static IEnumerable<Money> Generate()
		{
			var powersOfTwo = Enumerable.Range(0, 50).Select(x => (long)1 << x);
			var powersOfThree = Enumerable.Range(0, 32).Select(x => (long)Math.Pow(3, x));
			var powersOfTen = Enumerable.Range(0, 16).Select(x => (long)Math.Pow(10, x));

			var ternary = Multiple(new long[] { 1, 2 }, powersOfThree);
			var preferredValueSeries = Multiple(new long[] { 1, 2, 5 }, powersOfTen);

			return powersOfTwo
				.Select(x => Money.Satoshis(x))
				.Union(ternary)
				.Union(preferredValueSeries)
				.OrderBy(v => v);
		}
	}
}
