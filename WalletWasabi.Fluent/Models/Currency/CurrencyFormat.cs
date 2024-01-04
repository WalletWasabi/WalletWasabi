using System.Linq;
using System.Text.RegularExpressions;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Models.Currency;

/// <summary>
/// Represents a specific Currency's Parsing and Formatting rules.
/// </summary>
public partial class CurrencyFormat : ReactiveObject
{
	public static readonly CurrencyFormat Btc = new()
	{
		CurrencyCode = "BTC",
		IsApproximate = false,
		DefaultWatermark = "0 BTC",
		MaxFractionalDigits = 8,
		MaxIntegralDigits = 8,
		MaxLength = 20,
		Format = FormatBtcWithExactFractionals
	};

	public static readonly CurrencyFormat Usd = new()
	{
		CurrencyCode = "USD",
		IsApproximate = true,
		DefaultWatermark = "0.00 USD",
		MaxFractionalDigits = 2,
		MaxIntegralDigits = 12,
		MaxLength = 20,
		Format = FormatFiatWithExactFractionals,
	};

	public static readonly CurrencyFormat SatsvByte = new()
	{
		CurrencyCode = "sat/vByte",
		IsApproximate = false,
		MaxFractionalDigits = 2,
		MaxIntegralDigits = 8,
		MaxLength = 20,
		Format = FormatBtcWithExactFractionals
	};

	public string CurrencyCode { get; init; }
	public bool IsApproximate { get; init; }
	public int? MaxIntegralDigits { get; init; }
	public int? MaxFractionalDigits { get; init; }
	public int? MaxLength { get; init; }
	public Func<decimal, string> Format { get; init; }
	public string? DefaultWatermark { get; set; }

	/// <summary>
	/// Formats BTC values using as many fractional digits as they currently have.
	/// This is to avoid adding trailing zeros when typing values in the CurrencyEntryBox
	/// </summary>
	public static string FormatBtcWithExactFractionals(decimal amount)
	{
		var fractionalDigits = Math.Min(amount.CountFractionalDigits(), Btc.MaxFractionalDigits ?? 0);
		var fractionalString = "";
		for (var i = 0; i < fractionalDigits; i++)
		{
			fractionalString += "0";
			if (i == 3) // Leave an empty space after 4th character
			{
				fractionalString += " ";
			}
		}
		var fullString = $"{{0:### ### ### ##0.{fractionalString}}}";
		return string.Format(CurrencyInput.InvariantNumberFormat, fullString, amount).Trim();
	}

	/// <summary>
	/// Formats fiat values using as many fractional digits as they currently have.
	/// This is to avoid adding trailing zeros when typing values in the CurrencyEntryBox
	/// </summary>
	public static string FormatFiatWithExactFractionals(decimal amount)
	{
		var fractionalDigits = Math.Min(amount.CountFractionalDigits(), Usd.MaxFractionalDigits ?? 0);
		return amount.FormattedFiat($"N{fractionalDigits}");
	}

	/// <summary>
	/// Parses the text according to the format rules, and validates that it doesn't exceed the MaxIntegralDigits and MaxFractionalDigits, if specified.
	/// </summary>
	/// <param name="preComposedText"></param>
	/// <returns>The decimal value resulting from the parse</returns>
	public CurrencyFormatParseResult Parse(string preComposedText)
	{
		var parsable =
			preComposedText.Replace(CurrencyInput.GroupSeparator, "");

		if (parsable.Any(c => InvalidCharacters().IsMatch(c.ToString())))
		{
			return new CurrencyFormatParseResult.Nan();
		}

		parsable = InvalidCharacters().Replace(preComposedText, "");

		// Parse string value to decimal using Invariant Localization
		if (CurrencyInput.TryParse(parsable) is not { } value)
		{
			return new CurrencyFormatParseResult.Nan();
		}

		if (parsable.StartsWith(CurrencyInput.DecimalSeparator))
		{
			return new CurrencyFormatParseResult.Nan();
		}

		// reject negative numbers
		if (value < 0)
		{
			return new CurrencyFormatParseResult.OutOfRange(value);
		}

		// Reject numbers above the Max Integral number of Digits
		if (MaxIntegralDigits is { } maxIntegral && value.CountIntegralDigits() > maxIntegral)
		{
			return new CurrencyFormatParseResult.OutOfRange(value);
		}

		// Reject numbers above the Max Fractional number of Digits
		if (MaxFractionalDigits is { } maxFractional && value.CountFractionalDigits() > maxFractional)
		{
			return new CurrencyFormatParseResult.OutOfRange(value);
		}

		return new CurrencyFormatParseResult.Ok(value);
	}

	/// <summary>
	/// Used to clean any character except digits and decimal separator
	/// </summary>
	[GeneratedRegex($"[^0-9{CurrencyInput.DecimalSeparator}]")]
	private static partial Regex InvalidCharacters();
}

public abstract record CurrencyFormatParseResult
{
	public record Nan : CurrencyFormatParseResult;

	public record OutOfRange(decimal Value) : CurrencyFormatParseResult;

	public record Ok(decimal Value) : CurrencyFormatParseResult;
}
