using System.Reactive.Concurrency;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Infrastructure;

internal class ClipboardObserver
{
	public ClipboardObserver(WalletBalances walletBalances)
	{
		WalletBalances = walletBalances;
	}

	private WalletBalances WalletBalances { get; }

	public IObservable<string?> ClipboardUsdContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(
				WalletBalances.UsdBalance,
				(text, balanceUsd) =>
				{
					return ParseToUsd(text)
						.Ensure(n => n <= balanceUsd)
						.Ensure(n => n >= 1)
						.Ensure(n => n.CountDecimalPlaces() <= 2);
				})
			.Select(money => money?.ToString("0.00"));
	}

	public IObservable<string?> ClipboardBtcContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(
				WalletBalances.BtcBalance,
				(text, balance) => ParseToMoney(text).Ensure(m => m <= balance))
			.Select(money => money?.ToDecimal(MoneyUnit.BTC).FormattedBtc());
	}

	private static decimal? ParseToUsd(string text)
	{
		return decimal.TryParse(text, out var n) ? n : (decimal?)default;
	}

	private static Money? ParseToMoney(string text)
	{
		return Money.TryParse(text, out var n) ? n : default;
	}
}
