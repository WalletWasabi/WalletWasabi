using System.Collections.Generic;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.Models.Wallets;

public class TransactionModel : ITransactionModel
{
	private TransactionModel(TransactionSummary summary)
	{
		Inputs = summary.Inputs;
		Outputs = summary.Outputs;
	}

	public IEnumerable<Output> Outputs { get; set; }

	public IEnumerable<Input> Inputs { get; set; }

	public static TransactionModel Create(TransactionSummary summary)
	{
		return new TransactionModel(summary);
	}
}
