using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Parsers;

namespace WalletWasabi.Hwi.Models
{
	public class HwiOption : IEquatable<HwiOption>
	{
		private HwiOption(HwiOptions type, string argument = null)
		{
			Type = type;
			Arguments = argument;
		}

		public static HwiOption Debug => new HwiOption(HwiOptions.Debug);

		public static HwiOption Help => new HwiOption(HwiOptions.Help);
		public static HwiOption Interactive => new HwiOption(HwiOptions.Interactive);

		public static HwiOption TestNet => new HwiOption(HwiOptions.TestNet);
		public static HwiOption Version => new HwiOption(HwiOptions.Version);

		public HwiOptions Type { get; }
		public string Arguments { get; }

		public static HwiOption DevicePath(string devicePath)
		{
			devicePath = Guard.NotNullOrEmptyOrWhitespace(nameof(devicePath), devicePath, trim: true);
			return new HwiOption(HwiOptions.DevicePath, devicePath);
		}

		public static HwiOption DeviceType(HardwareWalletModels deviceType) => new HwiOption(HwiOptions.DeviceType, deviceType.ToHwiFriendlyString());

		public static HwiOption Fingerprint(HDFingerprint fingerprint) => new HwiOption(HwiOptions.Fingerprint, fingerprint.ToString());

		public static HwiOption Password(string password) => new HwiOption(HwiOptions.Password, password);

		#region Equality

		public override bool Equals(object obj) => Equals(obj as HwiOption);

		public bool Equals(HwiOption other) => this == other;

		public override int GetHashCode() => (Type, Arguments).GetHashCode();

		public static bool operator ==(HwiOption x, HwiOption y) => x?.Type == y?.Type && x?.Arguments == y?.Arguments;

		public static bool operator !=(HwiOption x, HwiOption y) => !(x == y);

		#endregion Equality
	}
}
