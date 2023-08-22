using NBitcoin;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Hwi.Models;

public class HwiEnumerateEntry
{
	public HwiEnumerateEntry(
		HardwareWalletModels model,
		string path,
		string? serialNumber,
		HDFingerprint? fingerprint,
		bool? needsPinSent,
		bool? needsPassphraseSent,
		string? error,
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

		// If a Coldcard is not initialized then the fingerprint is full of zeros.
		if (model == HardwareWalletModels.Coldcard && fingerprint.HasValue && fingerprint.Value.ToString() == "00000000")
		{
			Code = HwiErrorCode.DeviceNotInitialized;
		}
	}

	public HardwareWalletModels Model { get; }
	public string Path { get; }
	public string? SerialNumber { get; }
	public HDFingerprint? Fingerprint { get; }
	public bool? NeedsPinSent { get; }
	public bool? NeedsPassphraseSent { get; }
	public string? Error { get; }
	public HwiErrorCode? Code { get; }

	public WalletType WalletType =>
		Model switch
		{
			HardwareWalletModels.Coldcard or HardwareWalletModels.Coldcard_Simulator => WalletType.Coldcard,
			HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X or HardwareWalletModels.Ledger_Nano_S_Plus => WalletType.Ledger,
			HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_1_Simulator or HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_T_Simulator => WalletType.Trezor,
			_ => WalletType.Hardware
		};

	public bool IsInitialized()
	{
		// Check for error message, too, not only code, because the currently released version doesn't have error code. This can be removed if HWI > 1.0.1 version is updated.
		var notInitialized = (Code is { } && Code == HwiErrorCode.DeviceNotInitialized) || (Error?.Contains("Not initialized", StringComparison.OrdinalIgnoreCase) is true);
		return !notInitialized;
	}
}
