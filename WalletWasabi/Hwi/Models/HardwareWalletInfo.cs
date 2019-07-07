using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Hwi.Models
{
	public class HardwareWalletInfo
	{
		public HardwareWalletInfo(string masterFingerprint, string serialNumber, HardwareWalletType type, string path, string error)
		{
			try
			{
				Guard.NotNullOrEmptyOrWhitespace(nameof(masterFingerprint), masterFingerprint);
				var masterFingerprintBytes = ByteHelpers.FromHex(masterFingerprint);
				MasterFingerprint = new HDFingerprint(masterFingerprintBytes);
			}
			catch (ArgumentException)
			{
				MasterFingerprint = null;
			}

			SerialNumber = serialNumber;
			Type = type;
			Path = path;
			Error = error;

			Ready = true;
			Initialized = true;
			NeedPin = false;

			if (Error != null)
			{
				if (Error.Contains("Not initialized", StringComparison.OrdinalIgnoreCase))
				{
					Initialized = false;
				}
				else if (Type == HardwareWalletType.Ledger &&
					(Error.Contains("get_pubkey_at_path canceled", StringComparison.OrdinalIgnoreCase)
					|| Error.Contains("Invalid status 6f04", StringComparison.OrdinalIgnoreCase) // It comes when device asleep too.
					|| Error.Contains("Device is asleep", StringComparison.OrdinalIgnoreCase)))
				{
					Ready = false;
				}
				else if (Error.Contains("Unlock by using 'promptpin' and then 'sendpin'"))
				{
					NeedPin = true;
				}
			}
		}

		public HDFingerprint? MasterFingerprint { get; }
		public bool Initialized { get; }
		public bool NeedPin { get; }
		public bool Ready { get; }
		public string SerialNumber { get; }
		public HardwareWalletType Type { get; }
		public string Path { get; }
		public string Error { get; }
	}
}
