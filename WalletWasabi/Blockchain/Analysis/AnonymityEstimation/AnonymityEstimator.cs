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
		public static int EstimateAnonymitySet(Transaction tx, int outputIndex)
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

		public static int EstimateAnonymitySet(Transaction tx, uint outputIndex) => EstimateAnonymitySet(tx, (int)outputIndex);

		public static int EstimateAnonymitySet(Transaction tx, int outputIndex, ICoinsView allWalletCoins)
		{
			var spentOwnCoins = allWalletCoins.OutPoints(tx.Inputs.Select(x => x.PrevOut)).ToList();
			var numberOfOwnInputs = spentOwnCoins.Count();
			// If it's a normal tx that isn't self spent, nor a coinjoin, then anonymity should stripped if there was any and start from zero.
			// Note: this is only a good idea from WWII, with WWI we calculate anonsets from the point the coin first hit the wallet.
			// If all our inputs are ours and there are more than 1 outputs then it's not a self-spent and it's not a coinjoin.
			// This'll work, because we are only calculating anonset for our own coins and Wasabi doesn't generate tx that has more than one own outputs.
			// Note: a bit optimization to calculate this would be to actually use own output data, but that's a bit harder to get. Anyway the new algo would be as follows:
			// ... all the inputs must be ours AND there must be at least one output that isn't ours.
			if (numberOfOwnInputs == tx.Inputs.Count && tx.Outputs.Count > 1)
			{
				return 1;
			}

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

			return anonset;
		}

		public static int EstimateAnonymitySet(Transaction tx, uint outputIndex, ICoinsView allWalletCoins) => EstimateAnonymitySet(tx, (int)outputIndex, allWalletCoins);
	}
}
