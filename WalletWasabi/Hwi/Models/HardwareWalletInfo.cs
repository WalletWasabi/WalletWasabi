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
			Guard.NotNullOrEmptyOrWhitespace(nameof(masterFingerprint), masterFingerprint);
			var masterFingerPrintBytes = ByteHelpers.FromHex(masterFingerprint);
			MasterFingerprint = new HDFingerprint(masterFingerPrintBytes);
			SerialNumber = serialNumber;
			Type = type;
			Path = path;
			Error = error;
		}

		public HDFingerprint MasterFingerprint { get; }
		public string SerialNumber { get; }
		public HardwareWalletType Type { get; }
		public string Path { get; }
		public string Error { get; }
	}
}
