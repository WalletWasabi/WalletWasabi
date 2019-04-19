using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Hwi.Models
{
	public class HardwareWalletInfo
	{
		public HardwareWalletInfo(string fingerprint, string serialNumber, HardwareWalletType type, string path, string error)
		{
			MasterFingerprint = new HDFingerprint(ByteHelpers.FromHex(fingerprint));
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
