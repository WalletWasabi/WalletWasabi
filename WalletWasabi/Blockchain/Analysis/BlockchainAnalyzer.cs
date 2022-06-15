using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis;

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
			int startingOutputAnonset;
			var distinctWalletInputPubKeys = tx.WalletInputs.Select(x => x.HdPubKey).ToHashSet();

			if (inputCount == ownInputCount)
			{
				startingOutputAnonset = AnalyzeSelfSpendWalletInputs(distinctWalletInputPubKeys);

				AnalyzeSelfSpendWalletOutputs(tx, startingOutputAnonset);
			}
			else
			{
				AnalyzeCoinjoinWalletInputs(tx, out startingOutputAnonset, out int nonMixedAnonScore);

				AnalyzeCoinjoinWalletOutputs(tx, startingOutputAnonset, nonMixedAnonScore, distinctWalletInputPubKeys);
			}

			AdjustWalletInputs(tx, distinctWalletInputPubKeys, startingOutputAnonset);
		}

		AnalyzeClusters(tx);
	}

	private static void AnalyzeCoinjoinWalletInputs(SmartTransaction tx, out int mixedAnonScore, out int nonMixedAnonScore)
	{
		// Consolidation in coinjoins is the only type of consolidation that's acceptable,
		// because coinjoins are an exception from common input ownership heuristic.
		// Calculate weighted average.
		mixedAnonScore = (int)(tx.WalletInputs.Sum(x => x.HdPubKey.AnonymitySet * x.Amount) / tx.WalletInputs.Sum(x => x.Amount));

		nonMixedAnonScore = tx.WalletInputs.Min(x => x.HdPubKey.AnonymitySet);
	}

	private int AnalyzeSelfSpendWalletInputs(HashSet<HdPubKey> distinctWalletInputPubKeys)
	{
		int startingOutputAnonset = Intersect(distinctWalletInputPubKeys.Select(x => x.AnonymitySet));
		foreach (var key in distinctWalletInputPubKeys)
		{
			key.SetAnonymitySet(startingOutputAnonset);
		}

		return startingOutputAnonset;
	}

	/// <summary>
	/// Estimate input cluster anonymity set size, penalizing input consolidations to accounting for intersection attacks.
	/// </summary>
	private int Intersect(IEnumerable<int> anonsets)
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
		var intersectionAnonset = smallestAnon / Math.Max(1, intersectPenalty);

		// The minimum anonymity set size is 1, enforce it when the punishment is very large.
		var normalizedIntersectionAnonset = Math.Max(1, (int)intersectionAnonset);
		return normalizedIntersectionAnonset;
	}

	private void AnalyzeCoinjoinWalletOutputs(SmartTransaction tx, int startingMixedOutputAnonset, int startingNonMixedOutputAnonset, ISet<HdPubKey> distinctWalletInputPubKeys)
	{
		var indistinguishableWalletOutputs = tx.WalletOutputs
			.GroupBy(x => x.Amount)
			.ToDictionary(x => x.Key, y => y.Count());

		var indistinguishableOutputs = tx.Transaction.Outputs
			.GroupBy(x => x.ScriptPubKey)
			.ToDictionary(x => x.Key, y => y.Sum(z => z.Value))
			.GroupBy(x => x.Value)
			.ToDictionary(x => x.Key, y => y.Count());

		var inputCount = tx.Transaction.Inputs.Count;
		var ownInputCount = tx.WalletInputs.Count;

		foreach (var newCoin in tx.WalletOutputs)
		{
			var output = newCoin.TxOut;
			var equalOutputCount = indistinguishableOutputs[output.Value];
			var ownEqualOutputCount = indistinguishableWalletOutputs[output.Value];

			// Anonset gain cannot be larger than others' input count.
			var anonset = Math.Min(equalOutputCount - ownEqualOutputCount, inputCount - ownInputCount);

			// Picking randomly an output would make our anonset: total/ours.
			anonset /= ownEqualOutputCount;

			// If not anonset gain achieved on the output, then it's best to assume it's change.
			var startingOutputAnonset = anonset == 0 ? startingNonMixedOutputAnonset : startingMixedOutputAnonset;

			// Account for the inherited anonymity set size from the inputs in the
			// anonymity set size estimate.
			anonset += startingOutputAnonset;

			HdPubKey hdPubKey = newCoin.HdPubKey;
			uint256 txid = tx.GetHash();
			if (hdPubKey.AnonymitySet == HdPubKey.DefaultHighAnonymitySet)
			{
				// If the new coin's HD pubkey haven't been used yet
				// then its anonset haven't been set yet.
				// In that case the acquired anonset does not have to be intersected with the default anonset,
				// so this coin gets the acquired anonset.
				hdPubKey.SetAnonymitySet(anonset, txid);
			}
			else if (distinctWalletInputPubKeys.Contains(hdPubKey))
			{
				// If it's a reuse of an input's pubkey, then intersection punishment is senseless.
				hdPubKey.SetAnonymitySet(startingOutputAnonset, txid);
			}
			else if (tx.WalletOutputs.Where(x => x != newCoin).Select(x => x.HdPubKey).Contains(hdPubKey))
			{
				// If it's a reuse of another output's pubkey, then intersection punishment can only go as low as the inherited anonset.
				hdPubKey.SetAnonymitySet(Math.Max(startingOutputAnonset, Intersect(new[] { anonset, hdPubKey.AnonymitySet })), txid);
			}
			else if (hdPubKey.OutputAnonSetReasons.Contains(txid))
			{
				// If we already processed this transaction for this script
				// then we'll go with normal processing.
				// It may be a duplicated processing or new information arrived (like other wallet loaded)
				// If there are more anonsets already
				// then it's address reuse that we have already punished so leave it alone.
				if (hdPubKey.OutputAnonSetReasons.Count == 1)
				{
					hdPubKey.SetAnonymitySet(anonset, txid);
				}
			}
			else
			{
				// It's address reuse.
				hdPubKey.SetAnonymitySet(Intersect(new[] { anonset, hdPubKey.AnonymitySet }), txid);
			}
		}
	}

	/// <summary>
	/// Adjusts the anonset of the inputs to the newly calculated output anonsets.
	/// </summary>
	private static void AdjustWalletInputs(SmartTransaction tx, HashSet<HdPubKey> distinctWalletInputPubKeys, int startingOutputAnonset)
	{
		// Sanity check.
		if (!tx.WalletOutputs.Any())
		{
			return;
		}

		var smallestOutputAnonset = tx.WalletOutputs.Min(x => x.HdPubKey.AnonymitySet);
		if (smallestOutputAnonset < startingOutputAnonset)
		{
			foreach (var key in distinctWalletInputPubKeys)
			{
				key.SetAnonymitySet(smallestOutputAnonset);
			}
		}
	}

	private void AnalyzeSelfSpendWalletOutputs(SmartTransaction tx, int startingOutputAnonset)
	{
		foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
		{
			if (key.AnonymitySet == HdPubKey.DefaultHighAnonymitySet)
			{
				key.SetAnonymitySet(startingOutputAnonset, tx.GetHash());
			}
			else
			{
				key.SetAnonymitySet(Intersect(new[] { startingOutputAnonset, key.AnonymitySet }), tx.GetHash());
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
		// then we can assume that the money was sent to learn our inputs.
		// AND if there're outputs that go to someone else,
		// then we can assume that the people learnt our change outputs,
		// or at the very least assume that all the changes in the tx is ours.
		// For example even if the assumed change output is a payment to someone, a blockchain analyzer
		// probably would just assume it's ours and go on with its life.
		foreach (var key in tx.WalletInputs.Select(x => x.HdPubKey))
		{
			key.SetAnonymitySet(1);
		}
		foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
		{
			key.SetAnonymitySet(1, tx.GetHash());
		}
	}

	private void AnalyzeClusters(SmartTransaction tx)
	{
		foreach (var newCoin in tx.WalletOutputs)
		{
			// Forget clusters when no unique outputs created in coinjoins,
			// otherwise in half mixed wallets all the labels quickly gravitate into a single cluster
			// making pocket selection unusable.
			if (newCoin.HdPubKey.AnonymitySet < 2)
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
