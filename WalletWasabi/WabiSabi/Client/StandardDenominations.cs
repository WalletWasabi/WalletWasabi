using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client;

using Denominations = IEnumerable<long>;

public static class StandardDenominations
{
	public static Denominations Create(long max)
	{
		static long Pow(int b, int exp) => (long)Math.Pow(b, exp);

		static Denominations PowersOf(int b) =>
			Enumerable
				.Range(0, int.MaxValue)
				.Select(exp => Pow(b, exp));

		Denominations Multiply(int factor, Denominations denoms) =>
			denoms.Select(x => x * factor).TakeWhile(n => n < max);

		static Denominations Concat(Denominations first, params Denominations[] rest) =>
			rest.Length switch
			{
				0 => first,
				_ => Concat(Enumerable.Concat(first, rest[0]), rest[1..])
			};
		
		var denominations =
			Concat(
				Multiply(1, PowersOf( 2)),
				Multiply(1, PowersOf( 3)), Multiply(2, PowersOf( 3)),
				Multiply(1, PowersOf(10)), Multiply(2, PowersOf(10)), Multiply(5, PowersOf(10)));
		return denominations.Order().Distinct().ToList();
	}
}
