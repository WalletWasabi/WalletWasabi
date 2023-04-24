using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public static class TransactionSummaryExtensions
{
	public static Money OutputAmount(this TransactionSummary transaction) => transaction.Outputs.Sum(x => x.Amount);
	public static Money InputAmount(this TransactionSummary transaction) => transaction.Inputs.Sum(x => x.Amount);

	public static Money? Fee(this TransactionSummary transaction)
	{
		return transaction.InputAmount() - transaction.OutputAmount();
	}
}
