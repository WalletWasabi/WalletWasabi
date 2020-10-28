using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Analysis.AnonymityEstimation
{
	public static class AnonymityEstimator
	{
		public static int EstimateAnonymitySet(Transaction tx, uint outputIndex)
		{
			// 1. Get the output corresponting to the output index.
			var output = tx.Outputs[outputIndex];

			// 2. Get the number of equal outputs.
			int equalOutputs = tx.GetIndistinguishableOutputs(includeSingle: true).Single(x => x.value == output.Value).count;

			// 3. Anonymity set cannot be larger than the number of inputs.
			var inputCount = tx.Inputs.Count;
			var anonSet = Math.Min(equalOutputs, inputCount);
			return anonSet;
		}

		/// <returns>Dictionary of own output indexes and their calculated anonymity sets.</returns>
		public static IDictionary<uint, int> EstimateAnonymitySets(Transaction tx, IEnumerable<uint> ownOutputIndices, ICoinsView allWalletCoins)
		{
			// Estimation of anonymity sets only makes sense for own outputs.
			var numberOfOwnOutputs = ownOutputIndices.Count();
			if (numberOfOwnOutputs == 0)
			{
				return new Dictionary<uint, int>();
			}

			var spentOwnCoins = allWalletCoins.OutPoints(tx.Inputs.Select(x => x.PrevOut)).ToList();
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
				var anonset = EstimateAnonymitySet(tx, outputIndex);

				// If we provided inputs to the transaction.
				if (numberOfOwnInputs > 0)
				{
					// Take the input that we provided with the smallest anonset.
					// Our smallest anonset input is the relevant here, because this way the common input ownership heuristic is considered.
					var smallestInputAnon = spentOwnCoins.Min(x => x.AnonymitySet);

					// Punish consolidation exponentially.
					// If there is only a single input then the exponent should be zero to divide by 1 thus retain the input coin anonset.
					var consolidatePenalty = Math.Pow(2, numberOfOwnInputs - 1);
					var privacyBonus = smallestInputAnon / consolidatePenalty;

					// If the privacy bonus is <=1 then we are not inheriting any privacy from the inputs.
					var normalizedBonus = privacyBonus - 1;
					int sanityCheckedEstimation = (int)Math.Max(0d, normalizedBonus);

					// And add that to the base anonset from the tx.
					anonset += sanityCheckedEstimation;
				}

				// Factor in script reuse.
				var output = tx.Outputs[outputIndex];
				foreach (var coin in allWalletCoins.FilterBy(x => x.ScriptPubKey == output.ScriptPubKey))
				{
					anonset = Math.Min(anonset, coin.AnonymitySet);
					coin.AnonymitySet = anonset;
				}

				anonsets.Add(outputIndex, anonset);
			}
			return anonsets;
		}
	}
}
