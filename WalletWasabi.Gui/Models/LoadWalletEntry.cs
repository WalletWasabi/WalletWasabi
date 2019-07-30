using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Gui.Models
{
	public class LoadWalletEntry
	{
		public string WalletName { get; set; } = null;
		public HardwareWalletInfo HardwareWalletInfo { get; set; } = null;

		public LoadWalletEntry(string walletName)
		{
			WalletName = walletName;
			HardwareWalletInfo = null;
		}

		public LoadWalletEntry(HardwareWalletInfo hwi)
		{
			if (string.IsNullOrWhiteSpace(hwi.Error))
			{
				WalletName = hwi.Type.ToString();
			}
			else if (!hwi.Initialized)
			{
				WalletName = hwi.Type.ToString() + $" - Not Initialized";
			}
			else if (!hwi.Ready)
			{
				WalletName = hwi.Type.ToString() + $" - Device Not Ready";
			}
			else if (hwi.NeedPin)
			{
				WalletName = hwi.Type.ToString();
			}
			else
			{
				WalletName = hwi.Type.ToString() + $" - Error: {hwi.Error}";
			}

			HardwareWalletInfo = hwi;
		}

		public override string ToString()
		{
			return WalletName;
		}
	}
}
