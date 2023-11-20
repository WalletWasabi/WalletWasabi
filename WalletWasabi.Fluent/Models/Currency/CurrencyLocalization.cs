using System;
using System.Globalization;

namespace WalletWasabi.Fluent.Models.Currency;

// TODO: Remove .NET invariant localization settings and support whatever the user's settings are.
public static class CurrencyLocalization
{
	public const string DecimalSeparator = ".";
	public const string GroupSeparator = " ";

	public static NumberFormatInfo InvariantNumberFormat { get; } = new()
	{
		CurrencyGroupSeparator = GroupSeparator,
		CurrencyDecimalSeparator = DecimalSeparator,
		NumberGroupSeparator = GroupSeparator,
		NumberDecimalSeparator = DecimalSeparator
	};

	public static decimal? TryParse(string str) =>
		decimal.TryParse(str, NumberStyles.Number, InvariantNumberFormat, out var value)
			? value
			: null;

	/// <summary>
	/// Formats the value in a way that is compatible with the current localization settings.
	/// </summary>
	/// <remarks>Right now Wasabi uses .NET invariant localization settings, but if we remove that, then this method should return a string compatible with whatever the user's settings are.</remarks>
	public static string LocalizedFormat(decimal value)
	{
		// TODO: use CurrentUICulture here
		return value.ToString(InvariantNumberFormat);
	}
}
