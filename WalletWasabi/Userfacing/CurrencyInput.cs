using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
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

	[GeneratedRegex(@"[\d.,٫٬⎖·\']")]
	public static partial Regex RegexValidCharsOnly();

	[GeneratedRegex(@"^[\d]*[.,٫٬⎖·\']?[\d]+$")]
	public static partial Regex RegexValidInput();

	/// <summary>
	/// Checks input amount value and attempts to correct it if it is not valid.
	/// </summary>
	/// <param name="original">Amount to check and correct if needed.</param>
	/// <param name="correctedAmount">Amount representing <paramref name="original"/> if <paramref name="original"/> is deemed to be a valid amount string.</param>
	/// <returns>
	/// * True when original was modified and the result is stored in <paramref name="correctedAmount"/>.
	/// * False when original was not modified, or if the original was invalid and could not be corrected.
	/// </returns>
	public static bool TryCorrectAmount(string original, [NotNullWhen(true)] out string? correctedAmount)
	{
		// No corrections was done.
		if (original == "")
		{
			correctedAmount = null;
			return false;
		}

		var corrected = original.Replace(" ", "");

		// String was trimmed, so it was changed.
		if (corrected == "")
		{
			correctedAmount = "";
			return true;
		}

		// Initial minus is allowed, and it is removed.
		if (corrected.StartsWith('-'))
		{
			corrected = corrected[1..];
		}

		bool isValid = RegexValidInput().IsMatch(corrected);
		if (!isValid)
		{
			correctedAmount = null;
			return false;
		}

		// https://en.wikipedia.org/wiki/Decimal_separator
		corrected = corrected.Replace(",", DecimalSeparator);
		corrected = corrected.Replace("٫", DecimalSeparator);
		corrected = corrected.Replace("٬", DecimalSeparator);
		corrected = corrected.Replace("⎖", DecimalSeparator);
		corrected = corrected.Replace("·", DecimalSeparator);
		corrected = corrected.Replace("'", DecimalSeparator);

		if (corrected.StartsWith('.'))
		{
			corrected = "0" + corrected;
		}

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
			correctedAmount = corrected;
			return true;
		}

		correctedAmount = corrected;
		return false;
	}

	public static bool TryCorrectBitcoinAmount(string original, [NotNullWhen(true)] out string? best)
	{
		// It does not matter if the value was corrected or not.
		_ = TryCorrectAmount(original, out var corrected);

		if (corrected is null)
		{
			best = null;
			return false;
		}

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

		best = corrected;
		return false;
	}
}
