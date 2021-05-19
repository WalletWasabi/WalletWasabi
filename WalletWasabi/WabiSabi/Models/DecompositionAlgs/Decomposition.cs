using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.DecompositionAlgs
{
	public class Decomposition : IEnumerable<Money>
	{
		public List<Money> Amounts { get; }

		public Decomposition(IEnumerable<Money> amounts)
		{
			Amounts = amounts.OrderBy(c => c).ToList();
		}

		public void Extend(Money coin)
		{
			int index = Amounts.BinarySearch(coin);
			if (index < 0)
			{
				Amounts.Insert(~index, coin);
			}
		}

		public IEnumerator<Money> GetEnumerator()
		{
			return Amounts.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
