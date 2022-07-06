using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis;

public class BlockchainAnalyzer
{
	private static long[] StdDenoms = new[]
	{
		5000L, 6561L, 8192L, 10000L, 13122L, 16384L, 19683L, 20000L, 32768L, 39366L, 50000L, 59049L, 65536L, 100000L, 118098L,
		131072L, 177147L, 200000L, 262144L, 354294L, 500000L, 524288L, 531441L, 1000000L, 1048576L, 1062882L, 1594323L, 2000000L,
		2097152L, 3188646L, 4194304L, 4782969L, 5000000L, 8388608L, 9565938L, 10000000L, 14348907L, 16777216L, 20000000L,
		28697814L, 33554432L, 43046721L, 50000000L, 67108864L, 86093442L, 100000000L, 129140163L, 134217728L, 200000000L,
		258280326L, 268435456L, 387420489L, 500000000L, 536870912L, 774840978L, 1000000000L, 1073741824L, 1162261467L,
		2000000000L, 2147483648L, 2324522934L, 3486784401L, 4294967296L, 5000000000L, 6973568802L, 8589934592L, 10000000000L,
		10460353203L, 17179869184L, 20000000000L, 20920706406L, 31381059609L, 34359738368L, 50000000000L, 62762119218L,
		68719476736L, 94143178827L, 100000000000L, 137438953472L
	};

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
			double startingOutputAnonset;
			var distinctWalletInputPubKeys = tx.WalletInputs.Select(x => x.HdPubKey).ToHashSet();

			if (foreignInputCount == 0)
			{
				startingOutputAnonset = AnalyzeSelfSpendWalletInputs(distinctWalletInputPubKeys);

				AnalyzeSelfSpendWalletOutputs(tx, startingOutputAnonset);
			}
			else
			{
				AnalyzeCoinjoinWalletInputs(tx, out startingOutputAnonset, out double nonMixedAnonScore);

				AnalyzeCoinjoinWalletOutputs(tx, startingOutputAnonset, nonMixedAnonScore, distinctWalletInputPubKeys);
			}

			AdjustWalletInputs(tx, distinctWalletInputPubKeys, startingOutputAnonset);
		}

		AnalyzeClusters(tx);
	}

	private static void AnalyzeCoinjoinWalletInputs(SmartTransaction tx, out double mixedAnonScore, out double nonMixedAnonScore)
	{
		// Consolidation in coinjoins is the only type of consolidation that's acceptable,
		// because coinjoins are an exception from common input ownership heuristic.
		// Calculate weighted average.
		mixedAnonScore = tx.WalletInputs.Sum(x => x.HdPubKey.AnonymitySet * x.Amount.Satoshi) / tx.WalletInputs.Sum(x => x.Amount);

		nonMixedAnonScore = tx.WalletInputs.Min(x => x.HdPubKey.AnonymitySet);
	}

	private double AnalyzeSelfSpendWalletInputs(HashSet<HdPubKey> distinctWalletInputPubKeys)
	{
		double startingOutputAnonset = Intersect(distinctWalletInputPubKeys.Select(x => x.AnonymitySet));
		foreach (var key in distinctWalletInputPubKeys)
		{
			key.SetAnonymitySet(startingOutputAnonset);
		}

		return startingOutputAnonset;
	}

	/// <summary>
	/// Estimate input cluster anonymity set size, penalizing input consolidations to accounting for intersection attacks.
	/// </summary>
	private double Intersect(IEnumerable<double> anonsets)
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
		var normalizedIntersectionAnonset = Math.Max(1d, intersectionAnonset);
		return normalizedIntersectionAnonset;
	}

	private void AnalyzeCoinjoinWalletOutputs(SmartTransaction tx, double startingMixedOutputAnonset, double startingNonMixedOutputAnonset, ISet<HdPubKey> distinctWalletInputPubKeys)
	{
		var indistinguishableWalletOutputs = tx.WalletOutputs
			.GroupBy(x => x.Amount)
			.ToDictionary(x => x.Key, y => y.Count());

		var indistinguishableOutputs = tx.Transaction.Outputs
			.GroupBy(x => x.ScriptPubKey)
			.ToDictionary(x => x.Key, y => y.Sum(z => z.Value))
			.GroupBy(x => x.Value)
			.ToDictionary(x => x.Key, y => y.Count());

		var outputValues = tx.Transaction.Outputs.Select(x => x.Value).OrderByDescending(x => x).ToArray();
		var secondLargestOutputAmount = outputValues.Distinct().OrderByDescending(x => x).Take(2).Last();
		bool? isWasabi2Cj = null;

		var foreignInputCount = tx.ForeignInputs.Count;

		foreach (var newCoin in tx.WalletOutputs)
		{
			var output = newCoin.TxOut;
			var equalOutputCount = indistinguishableOutputs[output.Value];
			var ownEqualOutputCount = indistinguishableWalletOutputs[output.Value];

			// Anonset gain cannot be larger than others' input count.
			double anonset = Math.Min(equalOutputCount - ownEqualOutputCount, foreignInputCount);

			// Picking randomly an output would make our anonset: total/ours.
			anonset /= ownEqualOutputCount;

			// If no anonset gain achieved on the output, then it's best to assume it's change.
			double startingOutputAnonset;
			if (anonset < 1)
			{
				isWasabi2Cj ??=
					tx.Transaction.Inputs.Count >= 50 // 50 was the minimum input count at the beginning of Wasabi 2.
					&& outputValues.Count(x => StdDenoms.Contains(x.Satoshi)) > tx.Transaction.Outputs.Count * 0.8 // Most of the outputs contains the denomination.
					&& outputValues.SequenceEqual(outputValues); // Outputs are ordered descending.

				// When WW2 denom output isn't too large, then it's not change.
				if (isWasabi2Cj is true && StdDenoms.Contains(newCoin.Amount.Satoshi) && newCoin.Amount < secondLargestOutputAmount)
				{
					startingOutputAnonset = startingMixedOutputAnonset;
				}
				else
				{
					startingOutputAnonset = startingNonMixedOutputAnonset;
				}
			}
			else
			{
				startingOutputAnonset = startingMixedOutputAnonset;
			}

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
	private static void AdjustWalletInputs(SmartTransaction tx, HashSet<HdPubKey> distinctWalletInputPubKeys, double startingOutputAnonset)
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

	private void AnalyzeSelfSpendWalletOutputs(SmartTransaction tx, double startingOutputAnonset)
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
