using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System;

namespace WalletWasabi.WabiSabi.Client
{
	public class GreedyDecomposer
	{
		public GreedyDecomposer(IEnumerable<long> denominations)
		{
			_denominations = denominations.Any()
				? denominations.OrderByDescending(x => x).ToImmutableList()
				: throw new ArgumentException($"Argument {nameof(denominations)} has no elements");
		}

		private ImmutableList<long> _denominations;

		public IEnumerable<long> Decompose(long amount) =>
			Decompose(amount, _denominations.First(), _denominations.Skip(1), Enumerable.Empty<long>());

		private static IEnumerable<long> Decompose(long amount, long denomination, IEnumerable<long> denominations, IEnumerable<long> acc) =>
			(amount - denomination, denominations.Any()) switch
			{
				(0 or > 0, _) => Decompose(amount - denomination, denomination, denominations, acc.Append(denomination)),
				(< 0, true) => Decompose(amount, denominations.First(), denominations.Skip(1), acc),
				_           => acc
			};
	}
}

