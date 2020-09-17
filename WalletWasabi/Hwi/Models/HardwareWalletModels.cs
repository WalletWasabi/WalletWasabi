using System;
using System.ComponentModel;
using System.Linq;

namespace WalletWasabi.Hwi.Models
{
	/// <summary>
	/// Source: https://github.com/bitcoin-core/HWI/pull/228
	/// </summary>
	public enum HardwareWalletModels
	{
		[Description("Hardware Wallet")]
		Unknown,

		[Description("Coldcard")]
		Coldcard,

		[Description("Coldcard Simulator")]
		Coldcard_Simulator,

		[Description("BitBox")]
		DigitalBitBox_01,

		[Description("BitBox Simulator")]
		DigitalBitBox_01_Simulator,

		[Description("KeepKey")]
		KeepKey,

		[Description("KeepKeySimulator")]
		KeepKey_Simulator,

		[Description("Ledger Nano S")]
		Ledger_Nano_S,

		[Description("Trezor One")]
		Trezor_1,

		[Description("Trezor One Simulator")]
		Trezor_1_Simulator,

		[Description("Trezor T")]
		Trezor_T,

		[Description("Trezor T Simulator")]
		Trezor_T_Simulator,

		[Description("BitBox")]
		BitBox02_BTCOnly,

		[Description("BitBox")]
		BitBox02_Multi,
	}

	public static class EnumExtensions
	{
		public static T? GetFirstAttribute<T>(this Enum value) where T : Attribute
		{
			var type = value.GetType();
			var memberInfo = type.GetMember(value.ToString());
			var attributes = memberInfo[0].GetCustomAttributes(typeof(T), false);

			return attributes.Any() ? (T)attributes[0] : null;
		}

		public static string FriendlyName(this Enum value)
		{
			var attribute = value.GetFirstAttribute<DescriptionAttribute>();

			return attribute is { } ? attribute.Description : value.ToString();
		}
	}
}
