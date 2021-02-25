using System.ComponentModel;

namespace WalletWasabi.Hwi.Models
{
	/// <summary>
	/// Source: https://github.com/bitcoin-core/HWI/pull/228
	/// </summary>
	public enum HardwareWalletModels
	{
		[Description("Hardware Wallet")]
		Unknown,

		[Description("Coldcard")]
		Coldcard,

		[Description("Coldcard Simulator")]
		Coldcard_Simulator,

		[Description("BitBox")]
		DigitalBitBox_01,

		[Description("BitBox Simulator")]
		DigitalBitBox_01_Simulator,

		[Description("KeepKey")]
		KeepKey,

		[Description("KeepKey Simulator")]
		KeepKey_Simulator,

		[Description("Ledger Nano S")]
		Ledger_Nano_S,

		[Description("Ledger Nano X")]
		Ledger_Nano_X,

		[Description("Trezor One")]
		Trezor_1,

		[Description("Trezor One Simulator")]
		Trezor_1_Simulator,

		[Description("Trezor T")]
		Trezor_T,

		[Description("Trezor T Simulator")]
		Trezor_T_Simulator,

		[Description("BitBox")]
		BitBox02_BTCOnly,

		[Description("BitBox")]
		BitBox02_Multi,
	}
}
