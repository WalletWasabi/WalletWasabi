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
			if (cachedInputSanctions.ContainsKey(transactionOutput))
			{
				return cachedInputSanctions[transactionOutput];
			}

			SmartTransaction transaction = transactionOutput.Transaction;
			decimal sanction = CoinjoinAnalyzer.ComputeAnonymityContribution(transactionOutput, analyzedTransactionPrevOuts);
			sanction += transaction.WalletInputs.Select(ComputeInputSanctionHelper).DefaultIfEmpty(0).Max();
			cachedInputSanctions[transactionOutput] = sanction;
			return sanction;
		}

		return ComputeInputSanctionHelper(transactionInput);
	}

	public static decimal ComputeAnonymityContribution(SmartCoin transactionOutput, HashSet<OutPoint>? relevantOutpoints = null)
	{
		SmartTransaction transaction = transactionOutput.Transaction;
		IEnumerable<WalletVirtualOutput> walletVirtualOutputs = transaction.WalletVirtualOutputs;
		IEnumerable<ForeignVirtualOutput> foreignVirtualOutputs = transaction.ForeignVirtualOutputs;

		Money amount = walletVirtualOutputs.Where(o => o.Outpoints.Contains(transactionOutput.OutPoint)).First().Amount;
		Func<ForeignVirtualOutput, bool> isRelevantVirtualOutput = output => relevantOutpoints is null ? true : relevantOutpoints.Intersect(output.Outpoints).Any();

		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Where(o => o.Amount == amount).Count();
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Where(o => o.Amount == amount).Where(isRelevantVirtualOutput).Count();

		return (decimal)equalValueForeignRelevantVirtualOutputCount / equalValueWalletVirtualOutputCount;
	}
}
