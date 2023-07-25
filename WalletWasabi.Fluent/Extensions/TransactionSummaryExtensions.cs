using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionSummaryExtensions
{
	public static bool IsConfirmed(this TransactionSummary model)
	{
		var confirmations = model.GetConfirmations();
		return confirmations > 0;
	}

	public static int GetConfirmations(this TransactionSummary model) => model.Height.Type == HeightType.Chain ? (int)Services.BitcoinStore.SmartHeaderChain.ServerTipHeight - model.Height.Value + 1 : 0;

	public static MoneyUnit ToMoneyUnit(this FeeDisplayUnit feeDisplayUnit) =>
		feeDisplayUnit switch
		{
			FeeDisplayUnit.BTC => MoneyUnit.BTC,
			FeeDisplayUnit.Satoshis => MoneyUnit.Satoshi,
			_ => throw new InvalidOperationException($"Invalid Fee Display Unit value: {feeDisplayUnit}")
		};
}
