using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;

namespace WalletWasabi.Blockchain.Analysis;

public class CoinjoinAnalyzer
{
	public static readonly int MaxRecursionDepth = 3;
	public static readonly AggregationFunction Min = x => x.Any() ? x.Min(x => x.Anonymity) : 0;
	public static readonly AggregationFunction WeightedAverage = x => x.Any() ? x.WeightedAverage(x => x.Anonymity, x => x.Amount.Satoshi) : 0;

	public CoinjoinAnalyzer(SmartTransaction transaction)
	{
		_analyzedTransactionPrevOuts = transaction.Transaction.Inputs.Select(input => input.PrevOut).ToHashSet();
	}

	public delegate double AggregationFunction(IEnumerable<AmountWithAnonymity> amountWithAnonymity);

	private readonly HashSet<OutPoint> _analyzedTransactionPrevOuts;
	private readonly Dictionary<SmartCoin, double> _cachedInputSanctions = new();

	public double ComputeInputSanction(WalletVirtualInput virtualInput, AggregationFunction aggregationFunction)
		=> virtualInput.Coins.Select(x => ComputeInputSanction(x, aggregationFunction)).Max();

	public double ComputeInputSanction(SmartCoin transactionInput, AggregationFunction aggregationFunction)
	{
		double ComputeInputSanctionHelper(SmartCoin transactionOutput, int recursionDepth = 1)
		{
			if (recursionDepth > MaxRecursionDepth)
			{
				return 0;
			}

			// If we already analyzed the sanction for this output, then return the cached result.
			if (_cachedInputSanctions.TryGetValue(transactionOutput, out double value))
			{
				return value;
			}

			// Look at the transaction containing transactionOutput.
			// We are searching for any transaction inputs of analyzedTransaction that might have come from this transaction.
			// If we find such remixed outputs, then we determine how much they contributed to our anonymity set.
			SmartTransaction transaction = transactionOutput.Transaction;
			double sanction = -ComputeAnonymityContribution(transactionOutput, _analyzedTransactionPrevOuts);

			// Recursively branch out into all of the transaction inputs' histories and compute the sanction for each branch.
			// Add the worst-case branch to the resulting sanction.
			sanction += aggregationFunction(transaction.WalletInputs.Select(x => new AmountWithAnonymity(ComputeInputSanctionHelper(x, recursionDepth + 1), x.Amount)));

			// Cache the computed sanction in case we need it later.
			_cachedInputSanctions[transactionOutput] = sanction;
			return sanction;
		}

		return ComputeInputSanctionHelper(transactionInput);
	}

	/// <summary>
	/// Computes how much the foreign outputs of AnalyzedTransaction contribute to the anonymity of our transactionOutput.
	/// Sometimes we are only interested in how much a certain subset of foreign outputs contributed.
	/// This subset can be specified in relevantOutpoints, otherwise all outputs are considered relevant.
	/// </summary>
	public static double ComputeAnonymityContribution(SmartCoin transactionOutput, HashSet<OutPoint>? relevantOutpoints = null)
	{
		SmartTransaction transaction = transactionOutput.Transaction;
		IEnumerable<WalletVirtualOutput> walletVirtualOutputs = transaction.WalletVirtualOutputs;
		IEnumerable<ForeignVirtualOutput> foreignVirtualOutputs = transaction.ForeignVirtualOutputs;

		Money amount = walletVirtualOutputs.First(o => o.Coins.Select(c => c.Outpoint).Contains(transactionOutput.Outpoint)).Amount;
		bool IsRelevantVirtualOutput(ForeignVirtualOutput output) => relevantOutpoints is null || relevantOutpoints.Overlaps(output.OutPoints);

		// Count the outputs that have the same value as our transactionOutput.
		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Count(o => o.Amount == amount);
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Count(o => o.Amount == amount && IsRelevantVirtualOutput(o));

		// The anonymity set should increase by the number of equal-valued foreign outputs.
		// If we have multiple equal-valued wallet outputs, then we divide the increase evenly between them.
		// The rationale behind this is that picking randomly an output would make our anonset:
		// total/ours = 1 + foreign/ours, so the increase in anonymity is foreign/ours.
		return (double)equalValueForeignRelevantVirtualOutputCount / equalValueWalletVirtualOutputCount;
	}

	public record AmountWithAnonymity(double Anonymity, Money Amount);
}
