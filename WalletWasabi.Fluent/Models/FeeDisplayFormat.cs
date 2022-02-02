using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum FeeDisplayFormat
{
	BTC,

	[FriendlyName("sats")]
	Satoshis,
}

public static class FeeDisplayFormatExtensions
{
	public static MoneyUnit ToMoneyUnit(this FeeDisplayFormat feeDisplayFormat) =>
		feeDisplayFormat switch
		{
			FeeDisplayFormat.BTC => MoneyUnit.BTC,
			FeeDisplayFormat.Satoshis => MoneyUnit.Satoshi,
			_ => MoneyUnit.BTC
		};
}