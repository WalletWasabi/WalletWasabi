using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;

internal class WalletDashboardViewModelDesign : IWalletDashboardViewModel
{
	public IObservable<string>? BalanceUsd { get; } = Observable.Return("(≈2 000 USD)");
	public IObservable<string>? BalanceBtc { get; }= Observable.Return("0.804 2220 BTC");
	public IObservable<bool>? HasBalance { get; } = Observable.Return(true);
	public IObservable<string>? BtcToUsdExchangeRate { get; } = Observable.Return("99 000 USD");
	public IObservable<double>? PrivacyScore { get; } = Observable.Return(0.48);
	public IObservable<bool>? HasPrivacyScore { get; } = Observable.Return(true);
}