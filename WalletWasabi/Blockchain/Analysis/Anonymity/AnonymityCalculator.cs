using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.Analysis.Anonymity
{
	public class AnonymityCalculator
	{
		private ConcurrentDictionary<HdPubKey, double> Cache { get; } = new ConcurrentDictionary<HdPubKey, double>();

		public AnonymityCalculator(Money dustThreshold)
		{
			DustThreshold = dustThreshold;
		}

		public Money DustThreshold { get; }

		/// <param name="cachedResultOk">It's ok to return the value of the previous calculation.</param>
		public double Calculate(HdPubKey pubkey, bool cachedResultOk = false)
		{
			if (cachedResultOk && Cache.TryGetValue(pubkey, out double cachedValue))
			{
				return cachedValue;
			}

			// Address that nobody knows of has infinite anonymity set.
			double result = int.MaxValue;

			foreach (var coin in pubkey.Coins.Where(x => x.Amount > DustThreshold))
			{
				result = Math.Min(result, coin.Transaction.Transaction.GetAnonymitySet(coin.Index));
			}

			Cache.AddOrReplace(pubkey, result);
			return result;
		}
	}
}
