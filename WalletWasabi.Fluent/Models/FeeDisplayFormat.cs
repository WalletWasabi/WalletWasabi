using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum FeeDisplayFormat
{
	BTC,

	[FriendlyName("sat/vByte")]
	SatoshiPerByte,
}
