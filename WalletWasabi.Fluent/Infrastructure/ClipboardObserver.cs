using System.Reactive.Concurrency;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Infrastructure;

internal class ClipboardObserver
{
	private readonly IObservable<Amount> _balances;

	public ClipboardObserver(IObservable<Amount> balances)
	{
		_balances = balances;
	}

	public IObservable<string?> ClipboardUsdContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(_balances.Select(x => x.Usd).Switch(), ParseToUsd)
			.Select(money => money?.ToString("0.00"));
	}

	public IObservable<string?> ClipboardBtcContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(_balances.Select(x => x.Btc), ParseToMoney)
			.Select(money => money?.ToDecimal(MoneyUnit.BTC).FormattedBtc());
	}

	public static decimal? ParseToUsd(string? text)
	{
		if (text is null)
		{
			return null;
		}

		if (CurrencyInput.TryCorrectAmount(text, out var corrected))
		{
			text = corrected;
		}

		return decimal.TryParse(text, CurrencyInput.InvariantNumberFormat, out var n) ? n : (decimal?)default;
	}

	public static decimal? ParseToUsd(string? text, decimal balanceUsd)
	{
		return ParseToUsd(text)
			.Ensure(n => n <= balanceUsd)
			.Ensure(n => n >= 1)
			.Ensure(n => n.CountDecimalPlaces() <= 2);
	}

	public static Money? ParseToMoney(string? text)
	{
		if (text is null)
		{
			return null;
		}

		if (CurrencyInput.TryCorrectBitcoinAmount(text, out var corrected))
		{
			text = corrected;
		}

		return Money.TryParse(text, out var n) ? n : default;
	}

	public static Money? ParseToMoney(string? text, Money balance)
	{
		return ParseToMoney(text).Ensure(m => m <= balance);
	}
}
