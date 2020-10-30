using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis.AnonymityEstimation
{
	public class AnonymityEstimator
	{
		public AnonymityEstimator(CoinsRegistry allWalletCoins, AllTransactionStore transactionStore, KeyManager keyManager, Money dustThreshold)
		{
			AllWalletCoins = allWalletCoins;
			TransactionStore = transactionStore;
			KeyManager = keyManager;
			DustThreshold = dustThreshold;
		}

		public CoinsRegistry AllWalletCoins { get; }
		public AllTransactionStore TransactionStore { get; }
		public KeyManager KeyManager { get; }
		public Money DustThreshold { get; }

		/// <param name="updateOtherCoins">Only estimate -> does not touch other coins' anonsets.</param>
		/// <returns>Dictionary of own output indexes and their calculated anonymity sets.</returns>
		public IDictionary<uint, double> EstimateAnonymitySets(Transaction tx, bool updateOtherCoins = false)
		{
			var ownOutputs = new List<uint>();
			for (var i = 0U; i < tx.Outputs.Count; i++)
			{
				// If transaction received to any of the wallet keys:
				var output = tx.Outputs[i];
				HdPubKey foundKey = KeyManager.GetKeyForScriptPubKey(output.ScriptPubKey);
				if (foundKey is { })
				{
					ownOutputs.Add(i);
				}
			}

			return EstimateAnonymitySets(tx, ownOutputs, updateOtherCoins);
		}

		/// <param name="updateOtherCoins">Only estimate -> does not touch other coins' anonsets.</param>
		/// <returns>Dictionary of own output indexes and their calculated anonymity sets.</returns>
		public IDictionary<uint, double> EstimateAnonymitySets(Transaction tx, IEnumerable<uint> ownOutputIndices, bool updateOtherCoins = false)
		{
			// Estimation of anonymity sets only makes sense for own outputs.
			var numberOfOwnOutputs = ownOutputIndices.Count();
			if (numberOfOwnOutputs == 0)
			{
				return new Dictionary<uint, double>();
			}

			var allWalletCoinsView = AllWalletCoins.AsAllCoinsView();
			var spentOwnCoins = allWalletCoinsView.OutPoints(tx.Inputs.Select(x => x.PrevOut)).ToList();
			var numberOfOwnInputs = spentOwnCoins.Count();

			// In normal payments we expose things to our counterparties.
			// If it's a normal tx (that isn't self spent, nor a coinjoin,) then anonymity should be stripped.
			// All the inputs must be ours AND there must be at least one output that isn't ours.
			// Note: this is only a good idea from WWII, with WWI we calculate anonsets from the point the coin first hit the wallet.
			if (numberOfOwnInputs == tx.Inputs.Count)
			{
				if (tx.Outputs.Count > numberOfOwnOutputs)
				{
					var ret = new Dictionary<uint, double>();
					foreach (var outputIndex in ownOutputIndices)
					{
						ret.Add(outputIndex, 1);
					}
					return ret;
				}
			}

			var anonsets = new Dictionary<uint, double>();
			foreach (var outputIndex in ownOutputIndices)
			{
				// Get the anonymity set of i-th output in the transaction.
				var anonset = tx.GetAnonymitySet(outputIndex);

				// If we provided inputs to the transaction.
				if (numberOfOwnInputs > 0)
				{
					var privacyBonus = Intersect(spentOwnCoins.Select(x => x.AnonymitySet), (double)numberOfOwnInputs / tx.Inputs.Count());

					// If the privacy bonus is <=1 then we are not inheriting any privacy from the inputs.
					var normalizedBonus = privacyBonus - 1;

					// And add that to the base anonset from the tx.
					anonset += normalizedBonus;
				}

				// Factor in script reuse.
				var output = tx.Outputs[outputIndex];
				var reusedCoins = allWalletCoinsView.FilterBy(x => x.ScriptPubKey == output.ScriptPubKey).ToList();
				anonset = Intersect(reusedCoins.Select(x => x.AnonymitySet).Append(anonset), 1);

				// Dust attack could ruin the anonset of our existing mixed coins, so it's better not to do that.
				if (updateOtherCoins && output.Value > DustThreshold)
				{
					foreach (var coin in reusedCoins)
					{
						UpdateAnonset(coin, anonset);
					}
				}

				anonsets.Add(outputIndex, anonset);
			}
			return anonsets;
		}

		private void UpdateAnonset(SmartCoin coin, double anonset)
		{
			if (coin.AnonymitySet == anonset)
			{
				return;
			}

			coin.AnonymitySet = anonset;

			if (coin.SpenderTransactionId is { } && TransactionStore.TryGetTransaction(coin.SpenderTransactionId, out SmartTransaction childTx))
			{
				var anonymitySets = EstimateAnonymitySets(childTx.Transaction, updateOtherCoins: false);
				for (uint i = 0; i < childTx.Transaction.Outputs.Count; i++)
				{
					var allWalletCoinsView = AllWalletCoins.AsAllCoinsView();
					var childCoin = allWalletCoinsView.GetByOutPoint(new OutPoint(childTx.GetHash(), i));
					if (childCoin is { })
					{
						UpdateAnonset(childCoin, anonymitySets[i]);
					}
				}
			}
		}

		/// <param name="coefficient">If larger than 1, then penalty is larger, if smaller than 1 then penalty is smaller.</param>
		private double Intersect(IEnumerable<double> anonsets, double coefficient)
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
			var normalizedIntersectionAnonset = Math.Max(1d, intersectionAnonset);
			return normalizedIntersectionAnonset;
		}
	}
}
