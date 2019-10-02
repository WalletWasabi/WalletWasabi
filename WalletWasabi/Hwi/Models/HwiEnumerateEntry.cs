using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Hwi.Exceptions;

namespace WalletWasabi.Hwi.Models
{
	public class HwiEnumerateEntry
	{
		public HardwareWalletModels Model { get; }
		public string Path { get; }
		public string SerialNumber { get; }
		public HDFingerprint? Fingerprint { get; }
		public bool? NeedsPinSent { get; }
		public bool? NeedsPassphraseSent { get; }
		public string Error { get; }
		public HwiErrorCode? Code { get; }

		public bool IsInitialized()
		{
			// Check for error message, too, not only code, because the currently released version doesn't have error code. This can be removed if HWI > 1.0.1 version is updated.
			var notInitialized = (Code != null && Code == HwiErrorCode.DeviceNotInitialized) || (Error?.Contains("Not initialized", StringComparison.OrdinalIgnoreCase) is true);
			return !notInitialized;
		}

		public HwiEnumerateEntry(
			HardwareWalletModels model,
			string path,
			string serialNumber,
			HDFingerprint? fingerprint,
			bool? needsPinSent,
			bool? needsPassphraseSent,
			string error,
			HwiErrorCode? code)
		{
			Model = model;
			Path = path;
			SerialNumber = serialNumber;
			Fingerprint = fingerprint;
			NeedsPinSent = needsPinSent;
			NeedsPassphraseSent = needsPassphraseSent;
			Error = error;
			Code = code;
		}
	}
}
