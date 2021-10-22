using System.ComponentModel;

namespace WalletWasabi.Fluent.Models
{
	public enum FeeDisplayFormat
	{
		USD,
		BTC,

		[Description("sat/vByte")]
		SatoshiPerByte,

		Percentage
	}
}
