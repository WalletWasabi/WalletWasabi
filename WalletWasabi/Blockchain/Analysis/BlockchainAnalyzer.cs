using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
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
		var ownInputCount = tx.WalletInputs.Count;

		var foreignInputCount = tx.ForeignInputs.Count;
		var foreignOutputCount = tx.ForeignOutputs.Count;

		if (ownInputCount == 0)
		{
			AnalyzeReceive(tx);
		}
		else if (foreignInputCount == 0 && foreignOutputCount > 0)
		{
			AnalyzeNormalSpend(tx);
		}
		else
		{
			AnalyzeWalletInputs(tx, out int newInputAnonset, out int sanctionedInputAnonset);
			AdjustWalletInputsBefore(tx, newInputAnonset);

			if (foreignInputCount == 0)
			{
				AnalyzeSelfSpend(tx, newInputAnonset);
			}
			else
			{
				AnalyzeCoinjoin(tx, newInputAnonset, sanctionedInputAnonset);
			}

			AdjustWalletInputsAfter(tx, newInputAnonset);
		}

		AnalyzeClusters(tx);
	}

	/// <param name="newInputAnonset">The new anonymity set of the inputs.</param>
	private void AnalyzeWalletInputs(
		SmartTransaction tx,
		out int newInputAnonset,
		out int sanctionedInputAnonset)
	{
		// We want to weaken the punishment if the input merge happens in coinjoins.
		// Our strategy would be is to set the coefficient in proportion to our own inputs compared to the total inputs of the transaction.
		// However the accuracy can be increased if we consider every input with the same pubkey as a single input entity.
		// This we can only do for our own inputs as we don't know the pubkeys - nor the scripts - of other inputs.
		// Another way to think about this is: reusing pubkey on the input side is good, the punishment happened already.
		double coefficient = (double)tx.WalletVirtualInputs.Count / (tx.ForeignInputs.Count + tx.WalletVirtualInputs.Count);

		// Consolidation in coinjoins is the only type of consolidation that's acceptable,
		// because coinjoins are an exception from common input ownership heuristic.
		if (coefficient < 1)
		{
			// Calculate weighted average.
			var weightedAverage = (int)(tx.WalletVirtualInputs.Sum(x => x.HdPubKey.AnonymitySet * x.SmartCoins.Sum(y => y.Amount)) / tx.WalletVirtualInputs.Sum(x => x.SmartCoins.Sum(y => y.Amount)));
			newInputAnonset = (int)(weightedAverage - (weightedAverage * coefficient));
		}
		else
		{
			newInputAnonset = Intersect(tx.WalletVirtualInputs.Select(x => x.HdPubKey.AnonymitySet), coefficient);
		}

		// If we remix with siblings from earlier CoinJoins, then we must avoid double-counting their contribution to our anonymity set.
		// Search the history of each input to find any siblings with which we are remixing in the current transaction.
		// Discount the anonymity gain that we got from them earlier and are now losing due to the remix.
		CoinjoinAnalyzer coinjoinAnalyzer = new(tx);
		List<int> anonsets = new();
		foreach (var virtualInput in tx.WalletVirtualInputs)
		{
			anonsets.Add(virtualInput.SmartCoins.Select(i => Math.Max(1, virtualInput.HdPubKey.AnonymitySet - (int)coinjoinAnalyzer.ComputeInputSanction(i))).Min());
		}

		// Recompute newInputAnonset using the discounted anonsets.
		// This result is used as the base for computing the anonymity of this transaction's outputs.
		sanctionedInputAnonset = Intersect(anonsets, coefficient);
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

	private void AnalyzeCoinjoin(SmartTransaction tx, int newInputAnonset, int sanctionedInputAnonset)
	{
		var foreignInputCount = tx.ForeignInputs.Count;

		foreach (var newCoin in tx.WalletOutputs)
		{
			// Anonset gain cannot be larger than others' input count.
			var anonset = Math.Min((int)CoinjoinAnalyzer.ComputeAnonymityContribution(newCoin), foreignInputCount);

			// Account for the inherited anonymity set size from the inputs in the
			// anonymity set size estimate.
			anonset += sanctionedInputAnonset;

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
			else if (tx.WalletVirtualInputs.Select(i => i.HdPubKey).Contains(hdPubKey))
			{
				// If it's a reuse of an input's pubkey, then intersection punishment is senseless.
				hdPubKey.SetAnonymitySet(newInputAnonset, txid);
			}
			else if (tx.WalletOutputs.Where(x => x != newCoin).Select(x => x.HdPubKey).Contains(hdPubKey))
			{
				// If it's a reuse of another output's pubkey, then intersection punishment can only go as low as the inherited anonset.
				hdPubKey.SetAnonymitySet(Math.Max(newInputAnonset, Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1)), txid);
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
				hdPubKey.SetAnonymitySet(Intersect(new[] { anonset, hdPubKey.AnonymitySet }, 1), txid);
			}
		}
	}

	private static void AdjustWalletInputsBefore(SmartTransaction tx, int newInputAnonset)
	{
		foreach (var virtualInput in tx.WalletVirtualInputs)
		{
			virtualInput.HdPubKey.SetAnonymitySet(newInputAnonset);
		}
	}

	/// <summary>
	/// Adjusts the anonset of the inputs to the newly calculated output anonsets.
	/// </summary>
	private static void AdjustWalletInputsAfter(SmartTransaction tx, int newInputAnonset)
	{
		// Sanity check.
		if (!tx.WalletOutputs.Any())
		{
			return;
		}

		var smallestOutputAnonset = tx.WalletOutputs.Min(x => x.HdPubKey.AnonymitySet);
		if (smallestOutputAnonset < newInputAnonset)
		{
			foreach (var virtualInput in tx.WalletVirtualInputs)
			{
				virtualInput.HdPubKey.SetAnonymitySet(smallestOutputAnonset);
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
