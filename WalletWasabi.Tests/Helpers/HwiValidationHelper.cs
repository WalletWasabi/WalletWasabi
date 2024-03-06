using System.Text.RegularExpressions;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Tests.Helpers;

public static class HwiValidationHelper
{
	/// <summary>
	/// Parse the wallet's path from the response.
	/// </summary>
	/// <param name="path">The wallet path which come from HWI enumerate command.</param>
	/// <param name="model">The hardware wallet model.</param>
	/// <returns><c>true</c> if the path matches the model's regex, <c>false</c> otherwise.</returns>
	public static bool ValidatePathString(HardwareWalletModels model, string path)
	{
		string pattern = model switch
		{
			HardwareWalletModels.Trezor_T => "^webusb:",
			HardwareWalletModels.Trezor_1 => @"^hid:\\\\.*?vid_534c&pid_0001&mi_00",
			HardwareWalletModels.Coldcard => @"^hid:\\\\.*?vid_d13e&pid_cc10&mi_00",
			HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X => @"^hid:\\\\.*?vid_2c97&pid_0001&mi_00",
			HardwareWalletModels.Jade => @"^COM\d+",
			HardwareWalletModels.BitBox02_BTCOnly => @"^\\\\\?\\hid#vid_03eb&pid_2403",

			_ => "",
		};
		return Regex.IsMatch(path, pattern);
	}
}
