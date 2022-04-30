using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WalletWasabi.Fluent.Helpers;

public class LocalizedInputHelper
{
	private readonly CultureInfo _customCultureInfo;
	private readonly char _decimalSeparator = '.';
	private readonly char _groupSeparator = ' ';
	private readonly Regex _regexBtcFormat;
	private readonly Regex _regexDecimalCharsOnly;
	private readonly Regex _regexConsecutiveSpaces;
	private readonly Regex _regexGroupAndDecimal;

	public LocalizedInputHelper(char decimalSeparator, char groupSeparator)
	{
		_decimalSeparator = decimalSeparator;
		_groupSeparator = groupSeparator;

		_customCultureInfo = new CultureInfo("")
		{
			NumberFormat =
				{
					CurrencyGroupSeparator = _groupSeparator.ToString(),
					NumberGroupSeparator = _groupSeparator.ToString(),
					CurrencyDecimalSeparator = _decimalSeparator.ToString(),
					NumberDecimalSeparator = _decimalSeparator.ToString()
				}
		};

		_regexBtcFormat =
		new Regex(
			$"^(?<Whole>[0-9{_groupSeparator}]*)(\\{_decimalSeparator}?(?<Frac>[0-9{_groupSeparator}]*))$",
			RegexOptions.Compiled);

		_regexDecimalCharsOnly =
			new Regex(
				$"^[0-9{_groupSeparator}{_decimalSeparator}]*$", RegexOptions.Compiled);

		_regexConsecutiveSpaces =
			new Regex(
				$"{_groupSeparator}{{2,}}", RegexOptions.Compiled);

		_regexGroupAndDecimal =
			new Regex(
				$"[{_groupSeparator}{_decimalSeparator}]+", RegexOptions.Compiled);
	}

	public bool TryParse(string text, out decimal value, bool isFiat)
	{
		if (!ValidateEntryText(text, isFiat))
		{
			value = 0;
			return false;
		}

		if (decimal.TryParse(text.Replace($"{_groupSeparator}", ""), NumberStyles.Number, _customCultureInfo, out value))
		{
			return true;
		}

		return false;
	}

	public bool ValidateEntryText(string text, bool isFiat)
	{
		// Check if it has a decimal separator.
		var trailingDecimal = text.Length > 0 && text[^1] == _decimalSeparator;
		var match = _regexBtcFormat.Match(text);

		// Ignore group chars on count of the whole part of the decimal.
		var wholeStr = match.Groups["Whole"].ToString();
		var whole = _regexGroupAndDecimal.Replace(wholeStr, "").Length;

		var fracStr = match.Groups["Frac"].ToString().Replace($"{_groupSeparator}", "");
		var frac = _regexGroupAndDecimal.Replace(fracStr, "").Length;

		// Check for consecutive spaces (2 or more) and leading spaces.
		var rule1 = text.Length > 1 && (text[0] == _groupSeparator ||
												   _regexConsecutiveSpaces.IsMatch(text));

		// Check for trailing spaces in the whole number part and in the last part of the precomp string.
		var rule2 = whole >= 8 && (text.Last() == _groupSeparator || wholeStr.Last() == _groupSeparator);

		// Check for non-numeric chars.
		var rule3 = !_regexDecimalCharsOnly.IsMatch(text);
		if (rule1 || rule2 || rule3)
		{
			return false;
		}

		// Reject and dont process the input if the string doesnt match.
		if (!match.Success)
		{
			return false;
		}

		// Passthrough the decimal place char or the group separator.
		switch (text.Length)
		{
			case 1 when text[0] == _decimalSeparator && !trailingDecimal:
				return false;
		}

		if (isFiat)
		{
			// Fiat input restriction is to only allow 2 decimal places max
			// and also 16 whole number places.
			if ((whole > 16 && !trailingDecimal) || frac > 2)
			{
				return false;
			}
		}
		else
		{
			// Bitcoin input restriction is to only allow 8 decimal places max
			// and also 8 whole number places.
			if ((whole > 8 && !trailingDecimal) || frac > 8)
			{
				return false;
			}
		}

		return true;
	}
}
