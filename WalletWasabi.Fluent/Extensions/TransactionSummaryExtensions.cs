using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionSummaryExtensions
{
	public static bool IsConfirmed(this TransactionSummary model)
	{
		var confirmations = model.GetConfirmations();
		return confirmations > 0;
	}

	public static int GetConfirmations(this TransactionSummary model)
		=> model.Transaction.GetConfirmations((int)Services.SmartHeaderChain.ServerTipHeight);
}
