using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;

namespace WalletWasabi.Blockchain.Analysis;

public static class CoinjoinAnalyzer
{
	public static readonly AggregationFunction Min = x => x.Any() ? x.Min(x => x.Anonymity) : 0;
	public static readonly AggregationFunction WeightedAverage = x => x.Any() ? x.WeightedAverage(x => x.Anonymity, x => x.Amount.Satoshi) : 0;

	public delegate double AggregationFunction(IEnumerable<AmountWithAnonymity> amountWithAnonymity);

	public static double ComputeInputSanction(WalletVirtualInput virtualInput, SmartTransaction tx, AggregationFunction aggregationFunction)
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
	public static double ComputeAnonymityContribution(SmartCoin transactionOutput, HashSet<OutPoint> relevantOutpoints)
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

	public record AmountWithAnonymity(double Anonymity, Money Amount);
}
