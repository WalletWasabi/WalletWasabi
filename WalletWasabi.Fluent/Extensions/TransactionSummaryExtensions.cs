using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionSummaryExtensions
{
	public static bool IsConfirmed(this TransactionSummary model, uint serverHeight)
	{
		var confirmations = model.GetConfirmations(serverHeight);
		return confirmations > 0;
	}

	public static uint GetConfirmations(this TransactionSummary model, uint serverHeight)
		=> model.Transaction.GetConfirmations(serverHeight);
}
