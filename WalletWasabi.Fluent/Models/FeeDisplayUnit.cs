using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum FeeDisplayUnit
{
	BTC,

	[FriendlyName("sats")]
	Satoshis,
}
