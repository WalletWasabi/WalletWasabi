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
				? denominations.OrderByDescending(x => x).ToImmutableArray()
				: throw new ArgumentException($"Argument {nameof(denominations)} has no elements");
		}

		public ImmutableArray<Money> Denominations { get; }

		public IEnumerable<Money> Decompose(Money amount, Money costPerOutput)
		{
			for (var i = 0; i < Denominations.Length && costPerOutput < amount; i++)
			{
				var denomination = Denominations[i];

				while (denomination + costPerOutput <= amount)
				{
					amount -= denomination + costPerOutput;
					yield return denomination;
				}
			}
		}
	}
}
