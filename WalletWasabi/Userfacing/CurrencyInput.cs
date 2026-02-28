using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WalletWasabi.Userfacing;

public static partial class CurrencyInput
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

	[GeneratedRegex($"^[0-9{GroupSeparator}{DecimalSeparator}]*$")]
	public static partial Regex RegexDecimalCharsOnly();

	public static bool IsValidDecimal(string text, out decimal parsedValue)
	{
		if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, InvariantNumberFormat, out parsedValue))
		{
			return parsedValue >= 0;
		}

		return false;
	}

	public static bool TryCorrectBitcoinAmount(string original, [NotNullWhen(true)] out string? best)
	{
		if (!IsValidDecimal(original, out var parsedValue))
		{
			best = null;
			return false;
		}

		// Use at most 8 decimals.
		var corrected = parsedValue.ToString("0.########", CultureInfo.InvariantCulture);

		if (corrected != original)
		{
			best = corrected;
			return true;
		}

		best = corrected;
		return false;
	}
}
