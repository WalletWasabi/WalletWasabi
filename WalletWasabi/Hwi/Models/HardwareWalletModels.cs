using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Hwi.Models
{
	/// <summary>
	/// https://github.com/bitcoin-core/HWI/pull/228
	/// </summary>
	public enum HardwareWalletModels
	{
		Unknown,
		Coldcard,
		Coldcard_Simulator,
		DigitalBitBox_01,
		DigitalBitBox_01_Simulator,
		KeepKey,
		KeepKey_Simulator,
		Ledger_Nano_S,
		Trezor_1,
		Trezor_1_Simulator,
		Trezor_T,
		Trezor_T_Simulator,
	}
}
