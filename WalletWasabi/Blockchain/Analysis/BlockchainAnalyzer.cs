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

			if (ownInputCount == 0)
			{
				AnalyzeReceive(tx);
			}
			else if (inputCount == ownInputCount)
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
			AnalyzeWalletInputs(tx, out HashSet<HdPubKey> distinctWalletInputPubKeys, out int newInputAnonset);

			var indistinguishableWalletOutputs = tx
				.WalletOutputs.GroupBy(x => x.Amount)
				.ToDictionary(x => x.Key, y => y.Count());

			foreach (var newCoin in tx.WalletOutputs.ToArray())
			{
				// Get the anonymity set of i-th output in the transaction.
				var anonset = tx.Transaction.GetAnonymitySet(newCoin.Index);

				// Don't count our own equivalent outputs in the anonset.
				anonset -= indistinguishableWalletOutputs[newCoin.Amount] - 1;

				// We should only consider ourselves once in the anonset.
				anonset += newInputAnonset - 1;

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
					hdPubKey.AnonymitySet = newInputAnonset;
				}
				else if (tx.WalletOutputs.Where(x => x != newCoin).Select(x => x.HdPubKey).Contains(hdPubKey))
				{
					// If it's a reuse of another output' pubkey, then intersection punishment can only go as low as the inherited anonset.
					hdPubKey.AnonymitySet = Math.Max(newInputAnonset, Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1));
				}
				else
				{
					hdPubKey.AnonymitySet = Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1);
				}
			}
		}

		/// <param name="newInputAnonset">The new anonymity set of the inputs.</param>
		private void AnalyzeWalletInputs(SmartTransaction tx, out HashSet<HdPubKey> distinctWalletInputPubKeys, out int newInputAnonset)
		{
			// We want to weaken the punishment if the input merge happens in coinjoins.
			// Our strategy would be is to set the coefficient in proportion to our own inputs compared to the total inputs of the transaction.
			// However the accuracy can be increased if we consider every input with the same pubkey as a single input entity.
			// This we can only do for our own inputs as we don't know the pubkeys - nor the scripts - of other inputs.
			// Another way to think about this is: reusing pubkey on the input side is good, the punishment happened already.
			distinctWalletInputPubKeys = tx.WalletInputs.Select(x => x.HdPubKey).ToHashSet();
			var distinctWalletInputPubKeyCount = distinctWalletInputPubKeys.Count;
			var pubKeyReuseCount = tx.WalletInputs.Count - distinctWalletInputPubKeyCount;
			double coefficient = (double)distinctWalletInputPubKeyCount / (tx.Transaction.Inputs.Count - pubKeyReuseCount);

			newInputAnonset = Intersect(distinctWalletInputPubKeys.Select(x => x.AnonymitySet), coefficient);

			foreach (var key in distinctWalletInputPubKeys)
			{
				key.AnonymitySet = newInputAnonset;
			}
		}

		private static void AnalyzeReceive(SmartTransaction tx)
		{
			// No matter how much anonymity a user would had gained in a tx, if the money comes from outside, then make anonset 1.
			foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
			{
				key.AnonymitySet = 1;
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
			AnalyzeWalletInputs(tx, out _, out int inheritedAnonset);
			foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
			{
				key.AnonymitySet = inheritedAnonset;
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

		/// <summary>
		/// Estimate input cluster anonymity set size, penalizing input consolidations to accounting for intersection attacks.
		/// </summary>
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
