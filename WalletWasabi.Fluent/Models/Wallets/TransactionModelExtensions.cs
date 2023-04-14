using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.Models.Wallets;

public static class TransactionModelExtensions
{
	public static Money OutputAmount(this ITransactionModel transaction) => transaction.Outputs.Sum(x => x.Amount);
	public static Money? InputAmount(this ITransactionModel transaction) => transaction.Inputs.OfType<UnknownInput>().Any() ? null : transaction.Inputs.Cast<InputAmount>().Sum(x => x.Amount);

	public static Money? Fee(this ITransactionModel transaction)
	{
		if (transaction.InputAmount() is { } amount)
		{
			return amount - transaction.OutputAmount();
		}

		return null;
	}
}
