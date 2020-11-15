using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis
{
	public class BlockchainAnalyzer
	{
		public BlockchainAnalyzer(int privacyLevelThreshold)
		{
			PrivacyLevelThreshold = privacyLevelThreshold;
		}

		public int PrivacyLevelThreshold { get; }

		/// <summary>
		/// Sets clusters and anonymity sets for related HD public keys.
		/// </summary>
		public void Analyze(SmartTransaction tx)
		{
			var inputCount = tx.Transaction.Inputs.Count;
			var outputCount = tx.Transaction.Outputs.Count;

			var ownInputCount = tx.WalletInputs.Count;
			var ownOutputCount = tx.WalletOutputs.Count;

			if (inputCount == ownInputCount)
			{
				if (outputCount == ownOutputCount)
				{
					AnalyzeSelfSpend(tx);
				}
				else
				{
					AnalyzeNormalSpend(tx);
				}
			}
			else
			{
				AnalyzeCoinjoin(tx);
			}

			AnalyzeClusters(tx);
		}

		private void AnalyzeCoinjoin(SmartTransaction tx)
		{
			var ownInputCount = tx.WalletInputs.Count;
			var inputCount = tx.Transaction.Inputs.Count;
			var distinctWalletInputPubKeys = tx.WalletInputs.Select(x => x.HdPubKey).ToHashSet();
			var inheritedAnonset = 0;
			if (ownInputCount > 0)
			{
				// If we provided inputs to the transaction.
				// Reusing pubkey on the input side is good, the punishment happened already.
				var distinctWalletInputPubKeyCount = distinctWalletInputPubKeys.Count;
				var pubKeyReuseCount = ownInputCount - distinctWalletInputPubKeyCount;
				var privacyBonus = Intersect(distinctWalletInputPubKeys.Select(x => x.AnonymitySet), (double)distinctWalletInputPubKeyCount / (inputCount - pubKeyReuseCount));

				// If the privacy bonus is <=1 then we are not inheriting any privacy from the inputs.
				var normalizedBonus = privacyBonus - 1;

				// And add that to the base anonset from the tx.
				inheritedAnonset = normalizedBonus;

				foreach (var key in tx.WalletInputs.Select(x => x.HdPubKey))
				{
					key.AnonymitySet = inheritedAnonset + 1;
				}
			}

			var indistinguishableWalletOutputs = tx
				.WalletOutputs.GroupBy(x => x.Amount)
				.ToDictionary(x => x.Key, y => y.Count());

			foreach (var newCoin in tx.WalletOutputs)
			{
				// Get the anonymity set of i-th output in the transaction.
				var anonset = inheritedAnonset;

				// Calculating gained anonimity shall be limited by the number of inputs.
				// Although one person may create many equal outputs and it would seem like we successfully get lost
				// within this elephant, it's not the case as the elephant is more likely to ruin its own privacy, so let's not inflate anonsets for this.
				anonset += Math.Min(inputCount, tx.Transaction.GetAnonymitySet(newCoin.Index));

				// Don't create many anonset if we've provided a lot of equal outputs.
				anonset -= indistinguishableWalletOutputs[newCoin.Amount] - 1;

				HdPubKey hdPubKey = newCoin.HdPubKey;
				if (hdPubKey.AnonymitySet == HdPubKey.DefaultHighAnonymitySet)
				{
					// If the new coin's HD pubkey haven't been used yet
					// then its anonset haven't been set yet.
					// In that case the acquired anonset does not have to be intersected with the default anonset,
					// so this coin gets the aquired anonset.
					hdPubKey.AnonymitySet = anonset;
				}
				else if (distinctWalletInputPubKeys.Contains(hdPubKey))
				{
					// If it's a reuse of an input's pubkey, then intersection punishment is senseless.
					hdPubKey.AnonymitySet = inheritedAnonset + 1;
				}
				else
				{
					hdPubKey.AnonymitySet = Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1);
				}
			}
		}

		private static void AnalyzeNormalSpend(SmartTransaction tx)
		{
			// If all our inputs are ours and there's more than one output that isn't,
			// then we can assume that the persons the money was sent to learnt our inputs.
			// AND if there're outputs that go to someone else,
			// then we can assume that the people learnt our change outputs,
			// or at the very least assume that all the changes in the tx is ours.
			// For example even if the assumed change output is a payment to someone, a blockchain analyzer
			// probably would just assume it's ours and go on with its life.
			foreach (var key in tx.WalletInputs.Concat(tx.WalletOutputs).Select(x => x.HdPubKey))
			{
				key.AnonymitySet = 1;
			}
		}

		private void AnalyzeSelfSpend(SmartTransaction tx)
		{
			// If self spend then retain anonset:
			// Reusing pubkey on the input side is good, the punishment happened already.
			var distinctWalletInputPubKeys = tx.WalletInputs.Select(x => x.HdPubKey).ToHashSet();
			var anonset = Intersect(distinctWalletInputPubKeys.Select(x => x.AnonymitySet), 1);
			foreach (var key in tx.WalletInputs.Concat(tx.WalletOutputs).Select(x => x.HdPubKey))
			{
				key.AnonymitySet = anonset;
			}
		}

		private void AnalyzeClusters(SmartTransaction tx)
		{
			foreach (var newCoin in tx.WalletOutputs)
			{
				if (newCoin.HdPubKey.AnonymitySet < PrivacyLevelThreshold)
				{
					// Set clusters.
					foreach (var spentCoin in tx.WalletInputs)
					{
						newCoin.HdPubKey.Cluster.Merge(spentCoin.HdPubKey.Cluster);
					}
				}
			}
		}

		/// <param name="coefficient">If larger than 1, then penalty is larger, if smaller than 1 then penalty is smaller.</param>
		private int Intersect(IEnumerable<int> anonsets, double coefficient)
		{
			// Sanity check.
			if (!anonsets.Any())
			{
				return 1;
			}

			// Our smallest anonset is the relevant here, because anonsets cannot grow by intersection punishments.
			var smallestAnon = anonsets.Min();

			// Punish intersection exponentially.
			// If there is only a single anonset then the exponent should be zero to divide by 1 thus retain the input coin anonset.
			var intersectPenalty = Math.Pow(2, anonsets.Count() - 1);
			var intersectionAnonset = smallestAnon / Math.Max(1, intersectPenalty * coefficient);

			// Sanity check.
			var normalizedIntersectionAnonset = Math.Max(1, (int)intersectionAnonset);
			return normalizedIntersectionAnonset;
		}
	}
}
