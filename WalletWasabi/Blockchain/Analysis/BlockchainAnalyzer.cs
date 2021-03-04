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
			else if (inputCount == ownInputCount && outputCount != ownOutputCount)
			{
				AnalyzeNormalSpend(tx);
			}
			else
			{
				AnalyzeWalletInputs(tx, out HashSet<HdPubKey> distinctWalletInputPubKeys, out int newInputAnonset);

				if (inputCount == ownInputCount)
				{
					AnalyzeSelfSpend(tx, newInputAnonset);
				}
				else
				{
					AnalyzeCoinjoin(tx, newInputAnonset, distinctWalletInputPubKeys);
				}

				AdjustWalletInputs(tx, distinctWalletInputPubKeys, newInputAnonset);
			}

			AnalyzeClusters(tx);
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
				key.SetAnonymitySet(newInputAnonset, tx.GetHash());
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

			// The minimum anonymity set size is 1, enforce it when the punishment is very large.
			var normalizedIntersectionAnonset = Math.Max(1, (int)intersectionAnonset);
			return normalizedIntersectionAnonset;
		}

		private void AnalyzeCoinjoin(SmartTransaction tx, int newInputAnonset, ISet<HdPubKey> distinctWalletInputPubKeys)
		{
			var indistinguishableWalletOutputs = tx
				.WalletOutputs.GroupBy(x => x.Amount)
				.ToDictionary(x => x.Key, y => y.Count());

			var anonsets = tx.Transaction.GetAnonymitySets(tx.WalletOutputs.Select(x => x.Index));

			foreach (var newCoin in tx.WalletOutputs.ToArray())
			{
				// Begin estimating the anonymity set size based on the number of
				// equivalent outputs that the i-th output has in in the transaction.
				int anonset = anonsets[newCoin.Index];

				// Picking randomly an output would make our anonset: total/ours.
				anonset /= indistinguishableWalletOutputs[newCoin.Amount];

				// Account for the inherited anonymity set size from the inputs in the
				// anonymity set size estimate.
				// The anonymity set size estimated for the input cluster is corrected
				// by -1 to avoid double counting ourselves in the anonset.
				// Stated differently, the right value to use for the calculation is not the
				// anonymity set size, but the subset of only the other participants, since
				// every output must belong to a member of the set.
				anonset += newInputAnonset - 1;

				HdPubKey hdPubKey = newCoin.HdPubKey;
				uint256 txid = tx.GetHash();
				if (hdPubKey.AnonymitySet == HdPubKey.DefaultHighAnonymitySet)
				{
					// If the new coin's HD pubkey haven't been used yet
					// then its anonset haven't been set yet.
					// In that case the acquired anonset does not have to be intersected with the default anonset,
					// so this coin gets the aquired anonset.
					hdPubKey.SetAnonymitySet(anonset, txid);
				}
				else if (distinctWalletInputPubKeys.Contains(hdPubKey))
				{
					// If it's a reuse of an input's pubkey, then intersection punishment is senseless.
					hdPubKey.SetAnonymitySet(newInputAnonset, txid);
				}
				else if (tx.WalletOutputs.Where(x => x != newCoin).Select(x => x.HdPubKey).Contains(hdPubKey))
				{
					// If it's a reuse of another output' pubkey, then intersection punishment can only go as low as the inherited anonset.
					hdPubKey.SetAnonymitySet(Math.Max(newInputAnonset, Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1)), txid);
				}
				else if (hdPubKey.AnonymitySetReasons.Contains(txid))
				{
					// If we already processed this transaction for this script
					// then we'll go with normal processing, it's not an address reuse,
					// it's just we're processing the transaction twice.
					hdPubKey.SetAnonymitySet(anonset, txid);
				}
				else
				{
					// It's address reuse.
					hdPubKey.SetAnonymitySet(Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1), txid);
				}
			}
		}

		/// <summary>
		/// Adjusts the anonset of the inputs to the newly calculated output anonsets.
		/// </summary>
		private static void AdjustWalletInputs(SmartTransaction tx, HashSet<HdPubKey> distinctWalletInputPubKeys, int newInputAnonset)
		{
			// Sanity check.
			if (!tx.WalletOutputs.Any())
			{
				return;
			}

			var smallestOutputAnonset = tx.WalletOutputs.Min(x => x.HdPubKey.AnonymitySet);
			if (smallestOutputAnonset < newInputAnonset)
			{
				foreach (var key in distinctWalletInputPubKeys)
				{
					key.SetAnonymitySet(smallestOutputAnonset, tx.GetHash());
				}
			}
		}

		private void AnalyzeSelfSpend(SmartTransaction tx, int newInputAnonset)
		{
			foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
			{
				if (key.AnonymitySet == HdPubKey.DefaultHighAnonymitySet)
				{
					key.SetAnonymitySet(newInputAnonset, tx.GetHash());
				}
				else
				{
					key.SetAnonymitySet(Intersect(new[] { newInputAnonset, key.AnonymitySet }, 1), tx.GetHash());
				}
			}
		}

		private static void AnalyzeReceive(SmartTransaction tx)
		{
			// No matter how much anonymity a user would had gained in a tx, if the money comes from outside, then make anonset 1.
			foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
			{
				key.SetAnonymitySet(1, tx.GetHash());
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
				key.SetAnonymitySet(1, tx.GetHash());
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
	}
}
