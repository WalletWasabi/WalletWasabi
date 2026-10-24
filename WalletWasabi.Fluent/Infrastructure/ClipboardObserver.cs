using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Infrastructure;

internal class ClipboardObserver
{
	public static decimal? ParseToFiat(string? text)
	{
		if (text is null)
		{
			return null;
		}

		return decimal.TryParse(text, CurrencyInput.InvariantNumberFormat, out var n) ? n : (decimal?)default;
	}

	public static decimal? ParseToFiat(string? text, decimal balanceFiat)
	{
		return ParseToFiat(text)
			.Ensure(n => n <= balanceFiat)
			.Ensure(n => n >= 1)
			.Ensure(n => n.CountDecimalPlaces() <= 2);
	}

	public static Money? ParseToMoney(string? text)
	{
		if (text is null)
		{
			return null;
		}

		return Money.TryParse(text, out var n) ? n : default;
	}

	public static string? ParseToMoney(string? text, Money balance)
	{
		// Ignore paste if there are invalid characters
		if (text is null || !CurrencyInput.IsValidDecimal(text, out _))
		{
			return null;
		}

		var money = ParseToMoney(text).Ensure(m => m <= balance);
		return money?.ToDecimal(MoneyUnit.BTC).FormattedBtcExactFractional(text);
	}
}
