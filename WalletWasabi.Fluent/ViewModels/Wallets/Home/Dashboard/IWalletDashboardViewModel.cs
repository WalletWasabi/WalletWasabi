namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;

public interface IWalletDashboardViewModel
{
	IObservable<string>? BalanceUsd { get; }
	IObservable<string>? BalanceBtc { get; }
	IObservable<bool>? HasBalance { get; }
	IObservable<string>? BtcToUsdExchangeRate { get; }
	IObservable<double>? PrivacyScore { get; }
	IObservable<bool>? HasPrivacyScore { get; }
}