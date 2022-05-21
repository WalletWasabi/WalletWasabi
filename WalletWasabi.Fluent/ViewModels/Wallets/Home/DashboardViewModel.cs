using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home;

public partial class DashboardViewModel : ActivatableViewModel
{
	[AutoNotify] private string _btcPrice;

	private readonly IObservable<Unit> _balanceChanged;

	private readonly Wallet _wallet;

	public DashboardViewModel(WalletViewModel walletVM, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVM.Wallet;
		_btcPrice = "";
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(usd => BtcPrice = usd.FormattedFiat(format: "N0") + " USD")
			.DisposeWith(disposables);
	}
}