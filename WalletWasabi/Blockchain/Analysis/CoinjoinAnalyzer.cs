using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis;

public class CoinjoinAnalyzer
{
	private Dictionary<SmartCoin, decimal> cachedInputSanctions = new();
	private HashSet<OutPoint> analyzedTransactionPrevOuts;

	public CoinjoinAnalyzer(SmartTransaction analyzedTransaction)
	{
		AnalyzedTransaction = analyzedTransaction;
		analyzedTransactionPrevOuts = AnalyzedTransaction.Transaction.Inputs.Select(input => input.PrevOut).ToHashSet();
	}

	public SmartTransaction AnalyzedTransaction { get; }

	public decimal ComputeInputSanction(SmartCoin transactionInput)
	{
		decimal ComputeInputSanctionHelper(SmartCoin transactionOutput)
		{
			// If we already analyzed the sanction for this output, then return the cached result.
			if (cachedInputSanctions.ContainsKey(transactionOutput))
			{
				return cachedInputSanctions[transactionOutput];
			}

			// Look at the transaction containing transactionOutput.
			// We are searching for any transaction inputs of analyzedTransaction that might have come from this transaction.
			// If we find such remixed outputs, then we determine how much they contributed to our anonymity set.
			SmartTransaction transaction = transactionOutput.Transaction;
			decimal sanction = CoinjoinAnalyzer.ComputeAnonymityContribution(transactionOutput, analyzedTransactionPrevOuts);

			// Recursively branch out into all of the transaction inputs' histories and compute the sanction for each branch.
			// Add the worst-case branch to the resulting sanction.
			sanction += transaction.WalletInputs.Select(ComputeInputSanctionHelper).DefaultIfEmpty(0).Max();

			// Cache the computed sanction in case we need it later.
			cachedInputSanctions[transactionOutput] = sanction;
			return sanction;
		}

		return ComputeInputSanctionHelper(transactionInput);
	}

	// Computes how much the foreign outputs of AnalyzedTransaction contribute to the anonymity of our transactionOutput.
	// Sometimes we are only interested in how much a certain subset of foreign outputs contributed.
	// This subset can be specified in relevantOutpoints, otherwise all outputs are considered relevant.
	public static decimal ComputeAnonymityContribution(SmartCoin transactionOutput, HashSet<OutPoint>? relevantOutpoints = null)
	{
		SmartTransaction transaction = transactionOutput.Transaction;
		IEnumerable<WalletVirtualOutput> walletVirtualOutputs = transaction.WalletVirtualOutputs;
		IEnumerable<ForeignVirtualOutput> foreignVirtualOutputs = transaction.ForeignVirtualOutputs;

		Money amount = walletVirtualOutputs.Where(o => o.Outpoints.Contains(transactionOutput.OutPoint)).First().Amount;
		Func<ForeignVirtualOutput, bool> isRelevantVirtualOutput = output => relevantOutpoints is null ? true : relevantOutpoints.Intersect(output.Outpoints).Any();

		// Count the outputs that have the same value as our transactionOutput.
		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Where(o => o.Amount == amount).Count();
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Where(o => o.Amount == amount).Where(isRelevantVirtualOutput).Count();

		// The anonymity set should increase by the number of equal-valued foreign ouputs.
		// If we have multiple equal-valued wallet outputs, then we divide the increase evenly between them.
		// The rationale behind this is that picking randomly an output would make our anonset:
		// total/ours = 1 + foreign/ours, so the increase in anonymity is foreign/ours.
		return (decimal)equalValueForeignRelevantVirtualOutputCount / equalValueWalletVirtualOutputCount;
	}
}
