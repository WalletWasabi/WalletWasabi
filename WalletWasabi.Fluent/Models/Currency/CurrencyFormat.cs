using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
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
	};

	public static readonly CurrencyFormat Usd = new()
	{
		CurrencyCode = "USD",
		IsApproximate = true,
		DefaultWatermark = "0.00 USD",
		MaxFractionalDigits = 2,
		MaxIntegralDigits = 12,
		MaxLength = 20,
		ForceFractionalFormatToMaxFractionalDigits = true
	};

	public static readonly CurrencyFormat SatsvByte = new()
	{
		CurrencyCode = "sat/vByte",
		IsApproximate = false,
		MaxFractionalDigits = 2,
		MaxIntegralDigits = 8,
		MaxLength = 20,
	};

	public required string CurrencyCode { get; init; }
	public bool IsApproximate { get; init; }
	public int? MaxIntegralDigits { get; init; }
	public int? MaxFractionalDigits { get; init; }
	public int? MaxLength { get; init; }
	public string? DefaultWatermark { get; set; }
	public bool ForceFractionalFormatToMaxFractionalDigits { get; init; }
}

public abstract record CurrencyValue
{
	public static readonly Empty EmptyValue = new();

	public static CurrencyValue FromBtc(Amount? amount)
	{
		return amount switch
		{
			null => EmptyValue,
			Amount x when x == Amount.Invalid => new Invalid(),
			Amount x => new Valid(x.Btc.ToDecimal(NBitcoin.MoneyUnit.BTC))
		};
	}

	public static CurrencyValue FromUsd(Amount? amount)
	{
		return amount switch
		{
			null => EmptyValue,
			Amount x when x == Amount.Invalid => new Invalid(),
			Amount x => new Valid(x.UsdValue)
		};
	}

	public record Valid(decimal Value) : CurrencyValue;

	public record Invalid : CurrencyValue;

	public record Empty : CurrencyValue;
}
