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

		public IEnumerable<Money> Decompose(Money amount, Money costPerOutput)
		{
			var i = 0;
			var denomination = Denominations[i];
			while (amount - costPerOutput > Money.Zero)
			{
				if (denomination <= amount - costPerOutput)
				{
					yield return denomination;
					amount -= denomination + costPerOutput;
				}
				else if ((i + 1) < Denominations.Count)
				{
					i++;
					denomination = Denominations[i];
				}
				else
				{
					yield return amount;
					break;
				}
			}
		}
	}
}
