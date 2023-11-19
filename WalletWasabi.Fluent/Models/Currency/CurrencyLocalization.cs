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
}
