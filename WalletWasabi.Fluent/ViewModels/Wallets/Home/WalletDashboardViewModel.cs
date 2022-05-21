using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.Design;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home;

public partial class WalletDashboardViewModel : ActivatableViewModel, IWalletDashboardViewModel
{
	private readonly IObservable<Unit> _balanceChanged;

	private readonly Wallet _wallet;
	[AutoNotify] private string _btcPrice;

	public WalletDashboardViewModel(WalletViewModel walletVM, IObservable<Unit> balanceChanged)
	{
		_wallet = walletVM.Wallet;
		_btcPrice = "";

		Children = new[]
		{
			new FakeTileViewModel {Icon = "btc_regular", Title = "Balance", Value = "0.12345"},
			new FakeTileViewModel {Icon = "btc_regular", Title = "Balance", Value = "0.12345"},
			new FakeTileViewModel {Icon = "btc_regular", Title = "Balance", Value = "0.12345"},
		};
	}

	public ICollection<ViewModelBase> Children { get; set; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(usd => BtcPrice = usd.FormattedFiat("N0") + " USD")
			.DisposeWith(disposables);
	}
}