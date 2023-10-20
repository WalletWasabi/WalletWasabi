using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(
	Title = "Wallet Stats",
	Caption = "Display wallet stats",
	IconName = "nav_wallet_24_regular",
	Order = 3,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Stats", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletStatsViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	private readonly WalletViewModel _walletViewModel;

	[AutoNotify] private int _coinCount;
	[AutoNotify] private string _balance = "";
	[AutoNotify] private string _confirmedBalance = "";
	[AutoNotify] private string _unconfirmedBalance = "";
	[AutoNotify] private int _generatedKeyCount;
	[AutoNotify] private int _generatedCleanKeyCount;
	[AutoNotify] private int _generatedLockedKeyCount;
	[AutoNotify] private int _generatedUsedKeyCount;
	[AutoNotify] private int _totalTransactionCount;
	[AutoNotify] private int _nonCoinjointransactionCount;
	[AutoNotify] private int _coinjoinTransactionCount;

	private WalletStatsViewModel(WalletViewModel walletViewModel)
	{
		_wallet = walletViewModel.Wallet;
		_walletViewModel = walletViewModel;

		UpdateProps();

		NextCommand = ReactiveCommand.Create(() => Navigate().Clear());
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.FromEventPattern(_wallet, nameof(_wallet.WalletRelevantTransactionProcessed))
			.Subscribe(_ => UpdateProps())
			.DisposeWith(disposables);
	}

	private void UpdateProps()
	{
	}
}
