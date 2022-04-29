using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis;

public class CoinjoinAnalyzer
{
	private Dictionary<SmartCoin, decimal> cachedInputSanctions = new();

	public CoinjoinAnalyzer(SmartTransaction analyzedTransaction)
	{
		AnalyzedTransaction = analyzedTransaction;
	}

	public SmartTransaction AnalyzedTransaction { get; }

	public decimal ComputeInputSanction(SmartCoin transactionInput)
	{
		HashSet<OutPoint> analyzedTransactionPrevOuts = AnalyzedTransaction.Transaction.Inputs.Select(input => input.PrevOut).ToHashSet();

		decimal ComputeInputSanctionHelper(SmartCoin transactionOutput)
		{
			if (cachedInputSanctions.ContainsKey(transactionOutput))
			{
				return cachedInputSanctions[transactionOutput];
			}

			SmartTransaction transaction = transactionOutput.Transaction;
			decimal sanction = CoinjoinAnalyzer.ComputeAnonymityContribution(transactionOutput, analyzedTransactionPrevOuts);
			sanction += transaction.WalletInputs.Select(ComputeInputSanctionHelper).Sum();
			cachedInputSanctions[transactionOutput] = sanction;
			return sanction;
		}

		return ComputeInputSanctionHelper(transactionInput);
	}

	public static decimal ComputeAnonymityContribution(SmartCoin transactionOutput, HashSet<OutPoint>? relevantOutpoints = null)
	{
		SmartTransaction transaction = transactionOutput.Transaction;
		IEnumerable<VirtualOutput> walletVirtualOutputs = transaction.WalletVirtualOutputs;
		IEnumerable<VirtualOutput> foreignVirtualOutputs = transaction.ForeignVirtualOutputs;

		Money amount = walletVirtualOutputs.Where(o => o.Outpoints.Contains(transactionOutput.OutPoint)).First().Amount;
		Func<VirtualOutput, bool> isEqualValueVirtualOutput = x => x.Amount == amount;
		Func<VirtualOutput, bool> isRelevantVirtualOutput = output => relevantOutpoints is null ? true : relevantOutpoints.Intersect(output.Outpoints).Any();

		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Where(isEqualValueVirtualOutput).Count();
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Where(isEqualValueVirtualOutput).Where(isRelevantVirtualOutput).Count();

		return (decimal)equalValueForeignRelevantVirtualOutputCount / equalValueWalletVirtualOutputCount;
	}
}
