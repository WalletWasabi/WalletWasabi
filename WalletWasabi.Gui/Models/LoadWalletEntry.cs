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
			WalletName = hwi.Type.ToString();
			HardwareWalletInfo = hwi;
		}

		public override string ToString()
		{
			return WalletName;
		}
	}
}
