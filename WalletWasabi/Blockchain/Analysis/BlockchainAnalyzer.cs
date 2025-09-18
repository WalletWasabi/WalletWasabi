using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Analysis;

public static class BlockchainAnalyzer
{
	public static readonly long[] StdDenoms = new[]
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

	/// <summary>
	/// Sets clusters and anonymity sets for related HD public keys.
	/// </summary>
	public static void Analyze(SmartTransaction tx)
	{
		var ownInputCount = tx.WalletInputs.Count;

		var foreignInputCount = tx.ForeignInputs.Count;
		var foreignOutputCount = tx.ForeignOutputs.Count;

		AnalyzeCancellation(tx);

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

			if (foreignInputCount == 0)
			{
				startingOutputAnonset = AnalyzeSelfSpendWalletInputs(tx);

				AnalyzeSelfSpendWalletOutputs(tx, startingOutputAnonset);
			}
			else
			{
				var startingAnonScores = AnalyzeCoinjoinWalletInputs(tx);

				AnalyzeCoinjoinWalletOutputs(tx, startingAnonScores);

				startingOutputAnonset = startingAnonScores.WeightedAverage.standard;
			}

			AdjustWalletInputs(tx, startingOutputAnonset);
		}

		AnalyzeClusters(tx);
		SetIsSufficientlyDistancedFromExternalKeys(tx);
	}

	private static void AnalyzeCancellation(SmartTransaction tx)
	{
		// If the tx is a cancellation and we have at least one input or output that is not ours, then we set the anonset to 1.
		if (tx.IsCancellation && (tx.ForeignOutputs.Count != 0 || tx.ForeignInputs.Count != 0))
		{
			foreach (var k in tx.WalletInputs.Select(x => x.HdPubKey).Distinct())
			{
				k.SetAnonymitySet(1);
			}
		}
	}

	private static StartingAnonScores AnalyzeCoinjoinWalletInputs( SmartTransaction tx)
	{
		// Consolidation in coinjoins is the only type of consolidation that's acceptable,
		// because coinjoins are an exception from common input ownership heuristic.
		// However this is not always true:
		// For cases when it is we calculate weighted average.
		// For cases when it isn't we calculate the rest.
		var unmixed = CalculateMinAnonScore(tx);
		var mixed = CalculateWeightedAverage(tx);
		var halfMixed = CalculateHalfMixedAnonScore(tx, mixed.AnonScore, mixed.AnonScoreSanctioned);

		return new()
		{
			Minimum = unmixed,
			BigInputMinimum =  halfMixed,
			WeightedAverage = mixed
		};
	}

	private static (double, double) CalculateHalfMixedAnonScore(SmartTransaction tx, double mixedAnonScore, double mixedAnonScoreSanctioned)
	{
		// Calculate punishment to the smallest anonscore input from the largest inputs.
		// We know WW2 coinjoins order inputs by amount.
		var ourLargeHdPubKeys = new HashSet<HdPubKey>();
		for (uint i = 0; i < tx.Transaction.Inputs.Count; i++)
		{
			var currentInput = tx.Transaction.Inputs[i];
			var ourInput = tx.WalletInputs.FirstOrDefault(x => x.Outpoint == currentInput.PrevOut);
			if (ourInput is null)
			{
				// Don't look more.
				break;
			}
			ourLargeHdPubKeys.Add(ourInput.HdPubKey);
		}

		var largerWalletInputs = tx.WalletVirtualInputs.Where(x => ourLargeHdPubKeys.Contains(x.HdPubKey)).ToArray();
		var halfMixedAnonScore = largerWalletInputs.Select(x => x.HdPubKey.AnonymitySet).MinOrDefault();
		var halfMixedAnonScoreSanctioned = largerWalletInputs.Select(x => x.HdPubKey.AnonymitySet + ComputeInputMinSanction(x, tx)).MinOrDefault();

		// Sanity check: make sure to not give more than the weighted average would.
		return (Math.Min(halfMixedAnonScore, mixedAnonScore), Math.Min(halfMixedAnonScoreSanctioned, mixedAnonScoreSanctioned));
	}

	private static (double, double) CalculateMinAnonScore(SmartTransaction tx)
	{
		// Calculate punishment to the smallest anonscore input.
		return (tx.WalletVirtualInputs.Select(x => x.HdPubKey.AnonymitySet).MinOrDefault(),
			    tx.WalletVirtualInputs.Select(x => x.HdPubKey.AnonymitySet + ComputeInputMinSanction(x, tx)).MinOrDefault());
	}


	private static (double AnonScore, double AnonScoreSanctioned) CalculateWeightedAverage(SmartTransaction tx) =>
		// Calculate weighted average.
		(WeightedAverage(tx.WalletVirtualInputs.Select(x => new AmountWithAnonymity(x.HdPubKey.AnonymitySet, x.Amount))),
		 WeightedAverage(tx.WalletVirtualInputs.Select(x => new AmountWithAnonymity(x.HdPubKey.AnonymitySet + ComputeInputAvgSanction(x, tx), x.Amount))));


	private static double AnalyzeSelfSpendWalletInputs(SmartTransaction tx)
	{
		var distinctWalletInputPubKeys = tx.WalletVirtualInputs.Select(x => x.HdPubKey);
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
	private static double Intersect(IEnumerable<double> anonsets)
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

	private static void AnalyzeCoinjoinWalletOutputs(
		SmartTransaction tx,
		StartingAnonScores startingAnonScores)
	{
		var foreignInputCount = tx.ForeignInputs.Count;
		long? maxAmountWeightedAverageIsApplicableFor = null;

		foreach (var virtualOutput in tx.WalletVirtualOutputs)
		{
			(double standard, double sanctioned) startingOutputAnonset;

			// If the virtual output has a nonempty anonymity set
			if (tx.ForeignVirtualOutputs.All(x => x.Amount != virtualOutput.Amount))
			{
				// When WW2 denom output isn't too large, then it's not change.
				if (tx.IsWasabi2Cj && StdDenoms.Contains(virtualOutput.Amount.Satoshi))
				{
					if (maxAmountWeightedAverageIsApplicableFor is null && !TryGetLargestEqualForeignOutputAmount(tx, out maxAmountWeightedAverageIsApplicableFor))
					{
						maxAmountWeightedAverageIsApplicableFor = Constants.MaximumNumberOfSatoshis;
					}

					startingOutputAnonset = virtualOutput.Amount <= maxAmountWeightedAverageIsApplicableFor
						? startingAnonScores.WeightedAverage
						: startingAnonScores.BigInputMinimum;
				}
				else
				{
					startingOutputAnonset = startingAnonScores.Minimum;
				}
			}
			else
			{
				startingOutputAnonset = startingAnonScores.WeightedAverage;
			}

			// Anonset gain cannot be larger than others' input count.
			// Picking randomly an output would make our anonset: total/ours.
			double anonymityGain = Math.Min(ComputeAnonymityContribution(virtualOutput.Coins.First(), tx.ForeignVirtualOutputs.SelectMany(x => x.OutPoints).ToHashSet()), foreignInputCount);

			// Account for the inherited anonymity set size from the inputs in the
			// anonymity set size estimate.
			double anonset = new[] { startingOutputAnonset.sanctioned + anonymityGain, anonymityGain + 1, startingOutputAnonset.standard }.Max();

			foreach (var hdPubKey in virtualOutput.Coins.Select(x => x.HdPubKey).ToHashSet())
			{
				uint256 txid = tx.GetHash();
				if (Math.Abs(hdPubKey.AnonymitySet - HdPubKey.DefaultHighAnonymitySet) < 0.00001)
				{
					// If the new coin's HD pubkey haven't been used yet
					// then its anonset haven't been set yet.
					// In that case the acquired anonset does not have to be intersected with the default anonset,
					// so this coin gets the acquired anonset.
					hdPubKey.SetAnonymitySet(anonset, txid);
				}
				else if (tx.WalletVirtualInputs.Select(x => x.HdPubKey).Contains(hdPubKey))
				{
					// If it's a reuse of an input's pubkey, then intersection punishment is senseless.
					hdPubKey.SetAnonymitySet(startingOutputAnonset.sanctioned, txid);
				}
				else if (hdPubKey.HistoricalAnonSet.ContainsKey(txid))
				{
					// If we already processed this transaction for this script
					// then we'll go with normal processing.
					// It may be a duplicated processing or new information arrived (like other wallet loaded)
					// If there are more anonsets already
					// then it's address reuse that we have already punished so leave it alone.
					if (hdPubKey.HistoricalAnonSet.Count == 1)
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
	}

	private static bool TryGetLargestEqualForeignOutputAmount(SmartTransaction tx, [NotNullWhen(true)] out long? largestEqualForeignOutputAmount)
	{
		var found = tx
			.ForeignVirtualOutputs
			.Select(x => x.Amount.Satoshi)
			.GroupBy(x => x)
			.ToDictionary(x => x.Key, y => y.Count())
			.Select(x => (x.Key, x.Value))
			.FirstOrDefault(x => x.Value > 1).Key;

		largestEqualForeignOutputAmount = found == 0 ? null : found;

		return largestEqualForeignOutputAmount is not null;
	}

	/// <summary>
	/// Adjusts the anonset of the inputs to the newly calculated output anonsets.
	/// </summary>
	private static void AdjustWalletInputs(SmartTransaction tx, double startingOutputAnonset)
	{
		// Sanity check.
		if (tx.WalletOutputs.Count == 0)
		{
			return;
		}

		var smallestOutputAnonset = tx.WalletOutputs.Min(x => x.HdPubKey.AnonymitySet);
		if (smallestOutputAnonset < startingOutputAnonset)
		{
			foreach (var key in tx.WalletVirtualInputs.Select(x => x.HdPubKey))
			{
				key.SetAnonymitySet(smallestOutputAnonset);
			}
		}
	}

	private static void AnalyzeSelfSpendWalletOutputs(SmartTransaction tx, double startingOutputAnonset)
	{
		foreach (var key in tx.WalletOutputs.Select(x => x.HdPubKey))
		{
			if (Math.Abs(key.AnonymitySet - HdPubKey.DefaultHighAnonymitySet) < 0.000001)
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
		// AND if there are outputs that go to someone else,
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

	private static void AnalyzeClusters(SmartTransaction tx)
	{
		foreach (var newCoin in tx.WalletOutputs)
		{
			// Forget clusters when no unique outputs created in coinjoins,
			// otherwise in half mixed wallets all the labels quickly gravitate into a single cluster
			// making pocket selection unusable.
			if (newCoin.HdPubKey.AnonymitySet < Constants.SemiPrivateThreshold)
			{
				// Set clusters.
				foreach (var spentCoin in tx.WalletInputs)
				{
					newCoin.HdPubKey.Cluster.Merge(spentCoin.HdPubKey.Cluster);
				}
			}
		}
	}

	public static void SetIsSufficientlyDistancedFromExternalKeys(SmartTransaction tx)
	{
		foreach (var output in tx.WalletOutputs)
		{
			SetIsSufficientlyDistancedFromExternalKeys(output);
		}
	}

	/// <summary>
	/// Sets output's IsSufficientlyDistancedFromExternalKeys property to false if external, or the tx inputs are all external.
	/// </summary>
	/// <remarks>Context: https://github.com/WalletWasabi/WalletWasabi/issues/10567</remarks>
	public static void SetIsSufficientlyDistancedFromExternalKeys(SmartCoin output)
	{
		if (output.Transaction.WalletInputs.Count == 0)
		{
			// If there's no wallet input, then money is coming from external sources.
			output.IsSufficientlyDistancedFromExternalKeys = false;
		}
		else if (output.Transaction.WalletInputs.All(x => x.Transaction.WalletInputs.Count == 0))
		{
			// If there are wallet inputs, and each one of them are coming from external sources, then we consider this as not sufficiently distanced as well.
			output.IsSufficientlyDistancedFromExternalKeys = false;
		}
		else
		{
			output.IsSufficientlyDistancedFromExternalKeys = true;
		}
	}

	private delegate double AggregationFunction(IEnumerable<AmountWithAnonymity> amountWithAnonymity);

	private static double Min(IEnumerable<AmountWithAnonymity> x) => x.Select(y => y.Anonymity).MinOrDefault();
	private static double WeightedAverage(IEnumerable<AmountWithAnonymity> x) => x.WeightedAverage(y => y.Anonymity, y => y.Amount.Satoshi);

	private static double ComputeInputMinSanction(WalletVirtualInput virtualInput, SmartTransaction tx) =>
		ComputeInputSanction(virtualInput, tx, Min);

	private static double ComputeInputAvgSanction(WalletVirtualInput virtualInput, SmartTransaction tx) =>
		ComputeInputSanction(virtualInput, tx, WeightedAverage);

	private static double ComputeInputSanction(WalletVirtualInput virtualInput, SmartTransaction tx, AggregationFunction aggregationFunction)
		=> virtualInput.Coins.Select(x => ComputeInputSanction(x, tx, aggregationFunction)).Max();


	private static double ComputeInputSanction(SmartCoin transactionOutput, SmartTransaction tx, AggregationFunction aggregationFunction)
	{
		var relevantOutpoints = tx.Transaction.Inputs.Select(input => input.PrevOut).ToHashSet();

		double RecursiveComputeInputSanction(SmartCoin transactionOutput, int recursionDepth)
		{
			if (recursionDepth > 3)
			{
				return 0;
			}
			// Look at the transaction containing transactionOutput.
			// We are searching for any transaction inputs of analyzedTransaction that might have come from this transaction.
			// If we find such remixed outputs, then we determine how much they contributed to our anonymity set.
			SmartTransaction transaction = transactionOutput.Transaction;
			double sanction = -ComputeAnonymityContribution(transactionOutput, relevantOutpoints);

			// Recursively branch out into all of the transaction inputs' histories and compute the sanction for each branch.
			// Add the worst-case branch to the resulting sanction.
			sanction += aggregationFunction(transaction.WalletInputs.Select(x => new AmountWithAnonymity(RecursiveComputeInputSanction(x, recursionDepth + 1), x.Amount)));
			return sanction;
		}

		return RecursiveComputeInputSanction(transactionOutput, 1);
	}

	/// <summary>
	/// Computes how much the foreign outputs of AnalyzedTransaction contribute to the anonymity of our transactionOutput.
	/// Sometimes we are only interested in how much a certain subset of foreign outputs contributed.
	/// This subset can be specified in relevantOutpoints, otherwise all outputs are considered relevant.
	/// </summary>
	private static double ComputeAnonymityContribution(SmartCoin transactionOutput, HashSet<OutPoint> relevantOutpoints)
	{
		var walletVirtualOutputs = transactionOutput.Transaction.WalletVirtualOutputs;
		var foreignVirtualOutputs = transactionOutput.Transaction.ForeignVirtualOutputs;

		var amount = walletVirtualOutputs.SelectMany(o => o.Coins).First(c => c.Outpoint == transactionOutput.Outpoint) .Amount;

		// Count the outputs that have the same value as our transactionOutput.
		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Count(o => o.Amount == amount);
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Where(o => relevantOutpoints.Overlaps(o.OutPoints)).Count(o => o.Amount == amount);

		// The anonymity set should increase by the number of equal-valued foreign outputs.
		// If we have multiple equal-valued wallet outputs, then we divide the increase evenly between them.
		// The rationale behind this is that picking randomly an output would make our anonset:
		// total/ours = 1 + foreign/ours, so the increase in anonymity is foreign/ours.
		return (double)equalValueForeignRelevantVirtualOutputCount / equalValueWalletVirtualOutputCount;
	}

	private record AmountWithAnonymity(double Anonymity, Money Amount);
}
