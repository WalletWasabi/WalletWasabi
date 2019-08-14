using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Hwi2.Models;

namespace WalletWasabi.Gui.Models
{
	public class LoadWalletEntry
	{
		public string WalletName { get; set; } = null;
		public HwiEnumerateEntry HardwareWalletInfo { get; set; } = null;

		public LoadWalletEntry(string walletName)
		{
			WalletName = walletName;
			HardwareWalletInfo = null;
		}

		public LoadWalletEntry(HwiEnumerateEntry hwi)
		{
			string typeString = hwi.Type.ToString();
			var walletNameBuilder = new StringBuilder(typeString);

			if (!string.IsNullOrWhiteSpace(hwi.Error))
			{
				walletNameBuilder.Append($" - Error: {hwi.Error}");
			}
			else if (hwi.Code != null)
			{
				walletNameBuilder.Append($" - Error: {hwi.Code}");
			}
			else if (hwi.NeedsPinSent is true)
			{
				walletNameBuilder.Append($" - Needs Pin Sent");
			}
			else if (hwi.NeedsPassphraseSent is true)
			{
				walletNameBuilder.Append($" - Needs Passphrase Sent");
			}
			else if (hwi.Fingerprint is null)
			{
				walletNameBuilder.Append($" - Could Not Acquire Fingerprint");
			}

			WalletName = walletNameBuilder.ToString();
			HardwareWalletInfo = hwi;
		}

		public override string ToString()
		{
			return WalletName;
		}
	}
}
