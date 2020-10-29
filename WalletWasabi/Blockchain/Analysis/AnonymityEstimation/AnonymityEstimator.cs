using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Analysis.AnonymityEstimation
{
	public class AnonymityEstimator
	{
		public AnonymityEstimator(ICoinsView allWalletCoins, Money dustThreshold)
		{
			AllWalletCoins = allWalletCoins;
			DustThreshold = dustThreshold;
		}

		public ICoinsView AllWalletCoins { get; }
		public Money DustThreshold { get; }

		/// <param name="updateOtherCoins">Only estimate -> does not touch other coins' anonsets.</param>
		/// <returns>Dictionary of own output indexes and their calculated anonymity sets.</returns>
		public IDictionary<uint, int> EstimateAnonymitySets(Transaction tx, IEnumerable<uint> ownOutputIndices, bool updateOtherCoins = false)
		{
			// Estimation of anonymity sets only makes sense for own outputs.
			var numberOfOwnOutputs = ownOutputIndices.Count();
			if (numberOfOwnOutputs == 0)
			{
				return new Dictionary<uint, int>();
			}

			var spentOwnCoins = AllWalletCoins.OutPoints(tx.Inputs.Select(x => x.PrevOut)).ToList();
			var numberOfOwnInputs = spentOwnCoins.Count();

			// In normal payments we expose things to our counterparties.
			// If it's a normal tx (that isn't self spent, nor a coinjoin,) then anonymity should be stripped.
			// All the inputs must be ours AND there must be at least one output that isn't ours.
			// Note: this is only a good idea from WWII, with WWI we calculate anonsets from the point the coin first hit the wallet.
			if (numberOfOwnInputs == tx.Inputs.Count && tx.Outputs.Count > numberOfOwnOutputs)
			{
				var ret = new Dictionary<uint, int>();
				foreach (var outputIndex in ownOutputIndices)
				{
					ret.Add(outputIndex, 1);
				}
				return ret;
			}

			var anonsets = new Dictionary<uint, int>();
			foreach (var outputIndex in ownOutputIndices)
			{
				// Get the anonymity set of i-th output in the transaction.
				var anonset = tx.GetAnonymitySet(outputIndex);

				// If we provided inputs to the transaction.
				if (numberOfOwnInputs > 0)
				{
					var privacyBonus = Intersect(spentOwnCoins.Select(x => x.AnonymitySet));

					// If the privacy bonus is <=1 then we are not inheriting any privacy from the inputs.
					var normalizedBonus = privacyBonus - 1;

					// And add that to the base anonset from the tx.
					anonset += normalizedBonus;
				}

				// Factor in script reuse.
				var output = tx.Outputs[outputIndex];
				var reusedCoins = AllWalletCoins.FilterBy(x => x.ScriptPubKey == output.ScriptPubKey).ToList();
				anonset = Intersect(reusedCoins.Select(x => x.AnonymitySet).Append(anonset));

				// Dust attack could ruin the anonset of our existing mixed coins, so it's better not to do that.
				if (updateOtherCoins && output.Value > DustThreshold)
				{
					foreach (var coin in reusedCoins)
					{
						coin.AnonymitySet = anonset;
					}
				}

				anonsets.Add(outputIndex, anonset);
			}
			return anonsets;
		}

		private int Intersect(IEnumerable<int> anonsets)
		{
			// Our smallest anonset is the relevant here, because anonsets cannot grow by intersection punishments.
			var smallestAnon = anonsets.Min();
			// Punish intersection exponentially.
			// If there is only a single anonset then the exponent should be zero to divide by 1 thus retain the input coin anonset.
			var intersectPenalty = Math.Pow(2, anonsets.Count() - 1);
			var intersectionAnonset = smallestAnon / intersectPenalty;

			// Sanity check.
			var normalizedIntersectionAnonset = (int)Math.Max(1d, intersectionAnonset);
			return normalizedIntersectionAnonset;
		}
	}
}
