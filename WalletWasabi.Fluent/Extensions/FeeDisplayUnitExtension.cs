using NBitcoin;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Extensions;

public static class FeeDisplayUnitExtension
{
	public static MoneyUnit ToMoneyUnit(this FeeDisplayUnit feeDisplayUnit) =>
		feeDisplayUnit switch
		{
			FeeDisplayUnit.BTC => MoneyUnit.BTC,
			FeeDisplayUnit.Satoshis => MoneyUnit.Satoshi,
			_ => throw new InvalidOperationException($"Invalid Fee Display Unit value: {feeDisplayUnit}")
		};
}
