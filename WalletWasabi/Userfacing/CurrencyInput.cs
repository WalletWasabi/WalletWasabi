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

		var corrected = parsedValue.ToString(CultureInfo.InvariantCulture);

		// Enable max 8 decimals.
		var dotIndex = corrected.IndexOf('.');
		if (dotIndex != -1 && corrected.Length - (dotIndex + 1) > 8)
		{
			corrected = corrected[..(dotIndex + 1 + 8)];
		}

		if (corrected != original)
		{
			best = corrected;
			return true;
		}

		best = corrected;
		return false;
	}
}
