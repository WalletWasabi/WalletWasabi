using System.Text;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets
{
	public class HardwareWalletViewModel
	{
		public HardwareWalletViewModel(HwiEnumerateEntry hwi)
		{
			string typeString = hwi.Model.ToString();
			var walletNameBuilder = new StringBuilder(typeString);

			if (hwi.NeedsPinSent is true)
			{
				walletNameBuilder.Append(" - Needs PIN Sent");
			}
			else if (hwi.NeedsPassphraseSent is true)
			{
				walletNameBuilder.Append(" - Needs Passphrase Sent");
			}
			else if (!string.IsNullOrWhiteSpace(hwi.Error))
			{
				walletNameBuilder.Append($" - Error: {hwi.Error}");
			}
			else if (hwi.Code != null)
			{
				walletNameBuilder.Append($" - Error: {hwi.Code}");
			}
			else if (hwi.Fingerprint is null)
			{
				walletNameBuilder.Append(" - Could Not Acquire Fingerprint");
			}

			WalletName = walletNameBuilder.ToString();
			HardwareWalletInfo = hwi;
		}

		public string WalletName { get; }
		public HwiEnumerateEntry HardwareWalletInfo { get; }

		public override string ToString()
		{
			return WalletName;
		}
	}
}
