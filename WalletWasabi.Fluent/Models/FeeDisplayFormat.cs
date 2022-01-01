using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models
{
	public enum FeeDisplayFormat
	{
		USD,
		BTC,

		[FriendlyName("sat/vByte")]
		SatoshiPerByte,

		Percentage
	}
}
