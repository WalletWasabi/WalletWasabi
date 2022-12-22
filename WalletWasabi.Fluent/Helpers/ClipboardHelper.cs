using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Helpers;

internal class ClipboardHelper
{
	public ClipboardHelper(BalanceHelper balanceHelper)
	{
		BalanceHelper = balanceHelper;
	}

	private BalanceHelper BalanceHelper { get; }

	public IObservable<string?> ClipboardUsdContents()
	{
		return ApplicationHelper.ClipboardTextChanged(RxApp.MainThreadScheduler)
			.CombineLatest(
				BalanceHelper.UsdBalances,
				(text, balanceUsd) =>
				{
					return ParseToUsd(text).Ensure(n => n <= balanceUsd);
				})
			.Select(money => money?.ToString("0.00"));
	}

	public IObservable<string?> ClipboardBtcContents()
	{
		return ApplicationHelper.ClipboardTextChanged(RxApp.MainThreadScheduler)
			.CombineLatest(
				BalanceHelper.Balances,
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
