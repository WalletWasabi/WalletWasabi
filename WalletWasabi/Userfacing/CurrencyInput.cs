using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WalletWasabi.Helpers;

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

	[GeneratedRegex(@"[\d.,٫٬⎖·\']")]
	public static partial Regex RegexValidCharsOnly();

	public static bool TryCorrectAmount(string original, [NotNullWhen(true)] out string? best)
	{
		var corrected = original;

		// Correct amount
		Regex digitsOnly = new(@"[^\d.,٫٬⎖·\']");

		// Make it digits and .,٫٬⎖·\ only.
		corrected = digitsOnly.Replace(corrected, "");

		// https://en.wikipedia.org/wiki/Decimal_separator
		corrected = corrected.Replace(",", DecimalSeparator);
		corrected = corrected.Replace("٫", DecimalSeparator);
		corrected = corrected.Replace("٬", DecimalSeparator);
		corrected = corrected.Replace("⎖", DecimalSeparator);
		corrected = corrected.Replace("·", DecimalSeparator);
		corrected = corrected.Replace("'", DecimalSeparator);

		// Trim trailing dots except the last one.
		if (corrected.EndsWith('.'))
		{
			corrected = $"{corrected.TrimEnd('.')}.";
		}

		// Trim starting zeros.
		if (corrected.StartsWith('0'))
		{
			// If zeroless starts with a dot, then leave a zero.
			// Else trim all the zeros.
			var zeroless = corrected.TrimStart('0');
			if (zeroless.Length == 0)
			{
				corrected = "0";
			}
			else if (zeroless.StartsWith('.'))
			{
				corrected = $"0{corrected.TrimStart('0')}";
			}
			else
			{
				corrected = corrected.TrimStart('0');
			}
		}

		// Trim leading dots except the first one.
		if (corrected.StartsWith('.'))
		{
			corrected = $".{corrected.TrimStart('.')}";
		}

		// Do not enable having more than one dot.
		if (corrected.Count(x => x == '.') > 1)
		{
			// Except if it's at the end, we just remove it.
			corrected = corrected.TrimEnd('.');
			if (corrected.Count(x => x == '.') > 1)
			{
				corrected = "";
			}
		}

		if (corrected != original)
		{
			best = corrected;
			return true;
		}

		best = null;
		return false;
	}

	public static bool TryCorrectBitcoinAmount(string original, [NotNullWhen(true)] out string? best)
	{
		TryCorrectAmount(original, out var corrected);

		// If the original value wasn't fixed, it's definitely not a null.
		corrected ??= original;

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

		best = null;
		return false;
	}
}
