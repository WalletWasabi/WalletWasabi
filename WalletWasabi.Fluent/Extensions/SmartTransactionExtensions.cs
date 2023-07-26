using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.Extensions;

public static class SmartTransactionExtensions
{
	public static TimeSpan? GetConfirmationTime(this SmartTransaction transaction)
	{
		if (transaction.Confirmed)
		{
			return null;
		}

		if (Services.Synchronizer.LastAllFeeEstimate is { } allFeeEstimate)
		{
			if (allFeeEstimate.TryEstimateConfirmationTime(transaction, out var confirmationTime))
			{
				return confirmationTime;
			}
		}

		return null;
	}
}
