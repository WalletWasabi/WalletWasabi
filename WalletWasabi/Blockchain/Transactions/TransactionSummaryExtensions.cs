using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Blockchain.Transactions;

public static class TransactionSummaryExtensions
{
	public static Money OutputAmount(this TransactionSummary transaction) => transaction.Outputs.Sum(x => x.Amount);
	public static Money? InputAmount(this TransactionSummary transaction) => transaction.Inputs.OfType<UnknownInput>().Any() ? null : transaction.Inputs.Cast<InputAmount>().Sum(x => x.Amount);

	public static Money? Fee(this TransactionSummary transaction)
	{
		if (transaction.InputAmount() is null)
		{
			return null;
		}

		return transaction.InputAmount() - transaction.OutputAmount();
	}
}
