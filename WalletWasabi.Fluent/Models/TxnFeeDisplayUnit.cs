using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum TxnFeeDisplayUnit
{
	BTC,

	[FriendlyName("sats")]
	Satoshis,
}
