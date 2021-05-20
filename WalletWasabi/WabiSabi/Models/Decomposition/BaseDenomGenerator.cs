using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	public static class BaseDenominationGenerator
	{
		private const long MaxSats = 2099999997690000;

		private static IEnumerable<long> Multiple(IEnumerable<int> coefficients, IEnumerable<long> values)
		{
			foreach (var v in values)
			{
				foreach (var c in coefficients)
				{
					var x = c * v;
					if (x > MaxSats)
					{
						break;
					}
					yield return x;
				}
			}
		}

		public static IEnumerable<Money> Generate()
		{
			var powersOfTwo = Enumerable.Range(0, 50).Select(x => (long)1 << x);
			var powersOfThree = Enumerable.Range(0, 32).Select(x => (long)Math.Pow(3, x));
			var powersOfTen = Enumerable.Range(0, 16).Select(x => (long)Math.Pow(10, x));

			var ternary = Multiple(new[] { 1, 2 }, powersOfThree);
			var preferredValueSeries = Multiple(new[] { 1, 2, 5 }, powersOfTen);

			return powersOfTwo.Union(ternary).Union(preferredValueSeries).OrderBy(v => v).Select(v => Money.Satoshis(v));
		}
	}
}
