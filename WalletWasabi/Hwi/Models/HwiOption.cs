using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Parsers;

namespace WalletWasabi.Hwi.Models;

public class HwiOption : IEquatable<HwiOption>
{
	private HwiOption(HwiOptions type, string? argument = null)
	{
		Type = type;
		Arguments = argument;
	}

	public static HwiOption Debug => new(HwiOptions.Debug);

	public static HwiOption Help => new(HwiOptions.Help);
	public static HwiOption Interactive => new(HwiOptions.Interactive);

	public static HwiOption TestNet => new(HwiOptions.TestNet);
	public static HwiOption Version => new(HwiOptions.Version);
	public static HwiOption StdIn => new(HwiOptions.StdIn);

	public HwiOptions Type { get; }
	public string? Arguments { get; }

	public static HwiOption DevicePath(string devicePath)
	{
		devicePath = Guard.NotNullOrEmptyOrWhitespace(nameof(devicePath), devicePath, trim: true);
		return new HwiOption(HwiOptions.DevicePath, devicePath);
	}

	public static HwiOption DeviceType(HardwareWalletModels deviceType) => new(HwiOptions.DeviceType, deviceType.ToHwiFriendlyString());

	public static HwiOption Fingerprint(HDFingerprint fingerprint) => new(HwiOptions.Fingerprint, fingerprint.ToString());

	public static HwiOption Password(string password) => new(HwiOptions.Password, password);

	#region Equality

	public override bool Equals(object? obj) => Equals(obj as HwiOption);

	public bool Equals(HwiOption? other) => this == other;

	public override int GetHashCode() => (Type, Arguments).GetHashCode();

	public static bool operator ==(HwiOption? x, HwiOption? y) => (x?.Type, x?.Arguments) == (y?.Type, y?.Arguments);

	public static bool operator !=(HwiOption? x, HwiOption? y) => !(x == y);

	#endregion Equality
}
