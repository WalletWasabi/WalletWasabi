using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionSummaryExtensions
{
	public static bool IsConfirmed(this TransactionSummary model)
	{
		var confirmations = model.GetConfirmations();
		return confirmations > 0;
	}

	public static uint GetConfirmations(this TransactionSummary model)
		=> model.Transaction.GetConfirmations(Services.SmartHeaderChain.ServerTipHeight);
}
