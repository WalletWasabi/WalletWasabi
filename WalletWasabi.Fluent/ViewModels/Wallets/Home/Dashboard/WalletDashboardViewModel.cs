using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;

public class WalletDashboardViewModel : ActivatableViewModel, IWalletDashboardViewModel
{
	private readonly BalanceViewModel _balanceViewModel;
	private readonly PrivacyViewModel _privacyViewModel;

	public WalletDashboardViewModel(BalanceViewModel balanceViewModel, PrivacyViewModel privacyViewModel)
	{
		_balanceViewModel = balanceViewModel;
		_privacyViewModel = privacyViewModel;
	}

	public IObservable<string>? BalanceUsd => _balanceViewModel.BalanceUsd;

	public IObservable<string>? BalanceBtc => _balanceViewModel.BalanceBtc;

	public IObservable<bool>? HasBalance => _balanceViewModel.HasBalance;

	public IObservable<string>? BtcToUsdExchangeRate => _balanceViewModel.BtcToUsdExchangeRate;

	public IObservable<double>? PrivacyScore => _privacyViewModel.PrivacyScore;

	public IObservable<bool>? HasPrivacyScore => _privacyViewModel.HasPrivacyScore;

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_balanceViewModel.Activate(disposables);
		_privacyViewModel.Activate(disposables);
	}
}