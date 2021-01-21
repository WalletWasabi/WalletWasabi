using System.ComponentModel;

namespace WalletWasabi.Fluent.Model
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
