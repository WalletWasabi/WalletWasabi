using System.Collections.Generic;
using WalletWasabi.Helpers;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto;
using NBitcoin.Secp256k1;
using NBitcoin;

namespace System.Linq
{
	public static class Extensions
	{
		public static Scalar Sum(this IEnumerable<Scalar> scalars) =>
			scalars.Aggregate(Scalar.Zero, (s, acc) => s + acc);
			
		public static GroupElement Sum(this IEnumerable<GroupElement> groupElements) =>
			groupElements.Aggregate(GroupElement.Infinity, (ge, acc) => ge + acc);

		public static Money ToMoney(this Scalar scalar) =>
			Money.Satoshis(((ulong)scalar.d1 << 32) | scalar.d0);
			
		public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, IEnumerable<TThird> third, Func<TFirst, TSecond, TThird, TResult> resultSelector)
		{
			Guard.NotNull(nameof(first), first);
			Guard.NotNull(nameof(second), second);
			Guard.NotNull(nameof(third), third);
			Guard.NotNull(nameof(resultSelector), resultSelector);
			using var e1 = first.GetEnumerator();
			using var e2 = second.GetEnumerator();
			using var e3 = third.GetEnumerator();
			while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext())
			{
				yield return resultSelector(e1.Current, e2.Current, e3.Current);
			}
		}

		public static void CheckDimensions(this IEnumerable<Equation> equations, IEnumerable<ScalarVector> allResponses)
		{
			if (equations.Count() != allResponses.Count() ||
				Enumerable.Zip(equations, allResponses).Any(x => x.First.Generators.Count() != x.Second.Count()))
			{
				throw new ArgumentException("The number of responses and the number of generators in the equations do not match.");
			}
		}
	}
}
