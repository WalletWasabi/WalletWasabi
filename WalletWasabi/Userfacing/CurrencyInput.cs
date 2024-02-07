using System.Globalization;

namespace WalletWasabi.Userfacing;

public static class CurrencyInput
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

	public static decimal? TryParse(string str)
	{
		return decimal.TryParse(str, NumberStyles.Number, InvariantNumberFormat, out var value)
			? value
			: null;
	}

	public static string Format(decimal? value)
	{
		if (value is not { })
		{
			return string.Empty;
		}

		return value.Value.ToString(InvariantNumberFormat);
	}
}
