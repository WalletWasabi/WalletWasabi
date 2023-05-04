using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Infrastructure;

internal class ClipboardObserver
{
	private readonly IObservable<string?> _textChanged;

	public ClipboardObserver(WalletBalances walletBalances)
	{
		WalletBalances = walletBalances;
		_textChanged = ApplicationHelper.ClipboardTextChanged(RxApp.MainThreadScheduler)
			.Replay()
			.RefCount();
	}

	private WalletBalances WalletBalances { get; }

	public IObservable<string?> ClipboardUsdContentChanged()
	{
		return _textChanged
			.CombineLatest(
				WalletBalances.UsdBalance,
				(text, balanceUsd) => ParseToUsd(text)
						.Ensure(n => n <= balanceUsd)
						.Ensure(n => n >= 1)
						.Ensure(n => n.CountDecimalPlaces() <= 2))
			.Select(money => money?.ToString("0.00"));
	}

	public IObservable<string?> ClipboardBtcContentChanged()
	{
		return _textChanged
			.CombineLatest(
				WalletBalances.BtcBalance,
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
