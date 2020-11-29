using System.ComponentModel;

namespace WalletWasabi.Gui.Models
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
