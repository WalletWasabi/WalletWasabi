using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum FeeDisplayFormat
{
	BTC,

	[FriendlyName("sats")]
	Satoshis,
}