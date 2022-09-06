using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class TransactionModel
{
	private TransactionModel(TransactionSummary summary)
	{
		Inputs = summary.Inputs;
		Outputs = summary.Outputs;
	}

	public IEnumerable<Output> Outputs { get; set; }

	public IEnumerable<Blockchain.Transactions.Summary.Input> Inputs { get; set; }

	public static TransactionModel Create(TransactionSummary summary)
	{
		return new TransactionModel(summary);
	}
}

public static class TransactionModelExtensions
{
	public static Money OutputAmount(this TransactionModel transaction) => transaction.Outputs.Sum(x => x.Amount);
	public static Money? InputAmount(this TransactionModel transaction) => transaction.Inputs.OfType<UnknownInput>().Any() ? null : transaction.Inputs.Cast<InputAmount>().Sum(x => x.Amount);

	public static Money? Fee(this TransactionModel transaction)
	{
		if (InputAmount(transaction) is { } amount)
		{
			return amount - OutputAmount(transaction);
		}

		return null;
	}
}
