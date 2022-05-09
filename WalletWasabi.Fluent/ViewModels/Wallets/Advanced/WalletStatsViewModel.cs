using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(Title = "Wallet Statistics")]
public partial class WalletStatsViewModel : RoutableViewModel
{
	[AutoNotify] private int _coinCount;
	[AutoNotify] private string _balance = "";
	[AutoNotify] private string _confirmedBalance = "";
	[AutoNotify] private string _unconfirmedBalance = "";

	private readonly Wallet _wallet;

	public WalletStatsViewModel(WalletViewModelBase walletViewModelBase)
	{
		_wallet = walletViewModelBase.Wallet;

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
		// Number of coins in the wallet.
		CoinCount = _wallet.Coins.Unspent().Count();

        // Total amount of money in the wallet.
        Balance = $"{_wallet.Coins.TotalAmount().ToFormattedString()}";

        // Total amount of confirmed money in the wallet.
        ConfirmedBalance = $"{_wallet.Coins.Confirmed().TotalAmount().ToFormattedString()}";

        // Total amount of unconfirmed money in the wallet.
        UnconfirmedBalance = $"{_wallet.Coins.Unconfirmed().TotalAmount().ToFormattedString()}";
	}
}
