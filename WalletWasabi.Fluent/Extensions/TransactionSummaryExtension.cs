using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionSummaryExtension
{
	public static bool IsConfirmed(this TransactionSummary model)
	{
		var confirmations = model.Height.Type == HeightType.Chain ? (int)Services.BitcoinStore.SmartHeaderChain.TipHeight - model.Height.Value + 1 : 0;
		return confirmations > 0;
	}

	public static MoneyUnit ToMoneyUnit(this FeeDisplayFormat feeDisplayFormat) =>
		feeDisplayFormat switch
		{
			FeeDisplayFormat.BTC => MoneyUnit.BTC,
			FeeDisplayFormat.Satoshis => MoneyUnit.Satoshi,
			_ => throw new InvalidOperationException($"Invalid Fee Display Format value: {feeDisplayFormat}")
		};
}
