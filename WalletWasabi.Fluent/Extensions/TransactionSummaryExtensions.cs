using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionSummaryExtensions
{
	public static bool IsConfirmed(this TransactionSummary model, uint serverHeight)
	{
		var confirmations = model.GetConfirmations(serverHeight);
		return confirmations > 0;
	}

	public static uint GetConfirmations(this TransactionSummary model, uint serverHeight)
		=> model.Height is Height.ChainHeight(var height) ? serverHeight - height + 1 : 0;
}
