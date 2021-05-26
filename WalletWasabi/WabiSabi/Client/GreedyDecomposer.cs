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
			Denominations = denominations.Any()
				? denominations.OrderByDescending(x => x).ToImmutableList()
				: throw new ArgumentException($"Argument {nameof(denominations)} has no elements");
		}

		public ImmutableList<Money> Denominations { get; }

		public IEnumerable<Money> Decompose(Money amount)
		{
			var i = 0;
			var denomination = Denominations[i];
			while (amount > Money.Zero)
			{
				if (denomination <= amount)
				{
					yield return denomination;
					amount -= denomination;
				}
				else if (++i < Denominations.Count)
				{
					denomination = Denominations[i];
				}
				else
				{
					yield return amount;
				}
			}
		}
	}
}

