using System.Reactive.Concurrency;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Helpers;

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
					return ParseToUsd(text).Ensure(n => n <= balanceUsd);
				})
			.Select(money => money?.ToString("0.00"));
	}

	public IObservable<string?> ClipboardBtcContentChanged(IScheduler scheduler)
	{
		return ApplicationHelper.ClipboardTextChanged(scheduler)
			.CombineLatest(
				WalletBalances.BtcBalance,
				(text, balance) =>
				{
					return ParseToMoney(text)
						.Ensure(m => m <= balance);
				})
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
