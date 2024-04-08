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
			HardwareWalletModels.Trezor_1 => @"(^\\\\\?\\HID#VID_534C&PID_0001)|(DevSrvsID:\d+)|(\d+(-\d+\.\d+)+:\d+\.\d+)",
			HardwareWalletModels.Coldcard => @"(^\\\\\?\\HID#VID_D13E&PID_CC10)|(DevSrvsID:\d+)|(\d+(-\d+\.\d+)+:\d+\.\d+)",
			HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X => @"(^\\\\\?\\HID#VID_2C97&PID_0001)|(DevSrvsID:\d+)|(\d+(-\d+\.\d+)+:\d+\.\d+)",
			HardwareWalletModels.Jade => @"(^COM\d+)|(/dev/cu\.usbserial-[A-Za-z0-9]+)|(/dev/ttyACM\d+)",
			HardwareWalletModels.BitBox02_BTCOnly => @"(^\\\\\?\\HID#VID_03EB&PID_2403)|(DevSrvsID:\d+)|(\d+(-\d+\.\d+)+:\d+\.\d+)",
			_ => "",
		};
		return Regex.IsMatch(path, pattern);
	}
}
