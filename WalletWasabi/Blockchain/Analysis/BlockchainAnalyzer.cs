using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis
{
	public class BlockchainAnalyzer
	{
		public BlockchainAnalyzer(int privacyLevelThreshold, Money dustThreshold)
		{
			PrivacyLevelThreshold = privacyLevelThreshold;
			DustThreshold = dustThreshold;
		}

		public int PrivacyLevelThreshold { get; }
		public Money DustThreshold { get; }

		/// <summary>
		/// Sets clusters and anonymity sets for related HD public keys.
		/// </summary>
		public void Analyze(SmartTransaction tx)
		{
			// Dust attack could ruin the analysis, so it's better not to incorporate this.
			// For example if someone would dust a mixed coin of ours, its anon score would end up being 1,
			// even though the duster does not know who it sent the money.
			// FTR at the time of writing this comment the transaction processor doesn't even let dust to come this far,
			// but it's better to be prepared for future changes if it's not too computationally expensive.
			foreach (var newCoin in tx.WalletOutputs.Where(x => x.Amount > DustThreshold))
			{
				// Calculate anonymity sets.
				// Get the anonymity set of i-th output in the transaction.
				var anonset = tx.Transaction.GetAnonymitySet(newCoin.Index);
				// If we provided inputs to the transaction.
				if (tx.WalletInputs.Any())
				{
					// Take the input that we provided with the smallest anonset.
					// And add that to the base anonset from the tx.
					// Our smallest anonset input is the relevant here, because this way the common input ownership heuristic is considered.
					// Take minus 1, because we do not want to count own into the anonset, so...
					// If the anonset of our UTXO would be 1, and the smallest anonset of our inputs would be 1, too, then we don't make...
					// The new UTXO's anonset 2, but only 1.
					anonset += tx.WalletInputs.Select(x => x.HdPubKey).Min(x => x.AnonymitySet) - 1;
				}

				newCoin.HdPubKey.AnonymitySet = Math.Min(anonset, newCoin.HdPubKey.AnonymitySet);

				// Set clusters.
				if (newCoin.HdPubKey.AnonymitySet < PrivacyLevelThreshold)
				{
					foreach (var spentCoin in tx.WalletInputs)
					{
						newCoin.HdPubKey.Cluster.Merge(spentCoin.HdPubKey.Cluster);
					}
				}
			}
		}
	}
}
