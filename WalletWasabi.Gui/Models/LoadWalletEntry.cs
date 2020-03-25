using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Gui.Models
{
	public class LoadWalletEntry
	{
		public string WalletName { get; set; } = null;

		public LoadWalletEntry(string walletName)
		{
			WalletName = walletName;
		}

		public override string ToString()
		{
			return WalletName;
		}
	}
}
