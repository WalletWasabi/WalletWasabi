using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public class IndicatorsCellViewModel : ViewModelBase
{
	public IndicatorsCellViewModel(WalletCoinViewModel coin)
	{
		IsBanned = coin.WhenAnyValue(x => x.IsBanned).ReplayLastOnly();
		CoinJoinInProgress = coin.WhenAnyValue(x => x.CoinJoinInProgress).ReplayLastOnly();
		IsConfirmed = coin.WhenAnyValue(x => x.Confirmed).ReplayLastOnly();
		ConfirmedToolTip = coin.WhenAnyValue(x => x.ConfirmedToolTip).ReplayLastOnly();
		BannedUntilUtcToolTip = coin.WhenAnyValue(x => x.BannedUntilUtcToolTip).ReplayLastOnly();
	}

	public IObservable<string?> BannedUntilUtcToolTip { get; }

	public IObservable<bool> CoinJoinInProgress { get; }

	public IObservable<bool> IsConfirmed { get; }

	public IObservable<string?> ConfirmedToolTip { get; }

	public IObservable<bool> IsBanned { get; }
}
