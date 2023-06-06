using System.Reactive.Concurrency;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Infrastructure;

internal class ClipboardObserver
{
	public ClipboardObserver(IWalletBalancesModel walletBalances)
	{
		WalletBalances = walletBalances;
	}

	private IWalletBalancesModel WalletBalances { get; }

	public IObservable<string?> ClipboardUsdContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(
				WalletBalances.Usd,
				(text, balanceUsd) => ParseToUsd(text)
					.Ensure(n => n <= balanceUsd)
					.Ensure(n => n >= 1)
					.Ensure(n => n.CountDecimalPlaces() <= 2))
			.Select(money => money?.ToString("0.00"));
	}

	public IObservable<string?> ClipboardBtcContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(
				WalletBalances.Btc,
				(text, balance) => ParseToMoney(text).Ensure(m => m <= balance))
			.Select(money => money?.ToDecimal(MoneyUnit.BTC).FormattedBtc());
	}

	private static decimal? ParseToUsd(string? text)
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

	private static Money? ParseToMoney(string? text)
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
}
