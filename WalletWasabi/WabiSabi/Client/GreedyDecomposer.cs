using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Client
{
	public class GreedyDecomposer
	{
		public GreedyDecomposer(IEnumerable<Money> denominations)
		{
			_denominations = denominations.Any()
				? denominations.OrderByDescending(x => x).ToImmutableList()
				: throw new ArgumentException($"Argument {nameof(denominations)} has no elements");
		}

		private ImmutableList<Money> _denominations;

		public IEnumerable<Money> Decompose(Money amount)
		{
			var i = 0;
			var denomination = _denominations[i];
			while (amount > Money.Zero)
			{
				if (denomination <= amount)
				{
					yield return denomination;
					amount -= denomination;
				}
				else if(++i < _denominations.Count)
				{
					denomination = _denominations[i];
				}
				else
				{
					yield return amount;
				}
			}
		}
	}
}

