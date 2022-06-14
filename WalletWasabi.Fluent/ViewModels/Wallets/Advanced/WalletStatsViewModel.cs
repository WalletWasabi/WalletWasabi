using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(Title = "Wallet Statistics")]
public partial class WalletStatsViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;

	[AutoNotify] private int _coinCount;
	[AutoNotify] private string _balance = "";
	[AutoNotify] private string _confirmedBalance = "";
	[AutoNotify] private string _unconfirmedBalance = "";
	[AutoNotify] private int _generatedKeyCount;
	[AutoNotify] private int _generatedCleanKeyCount;
	[AutoNotify] private int _generatedLockedKeyCount;
	[AutoNotify] private int _generatedUsedKeyCount;
	[AutoNotify] private int _largestExternalKeyGap;
	[AutoNotify] private int _largestInternalKeyGap;

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

		GeneratedKeyCount = _wallet.KeyManager.GetKeys().Count();
		GeneratedCleanKeyCount = _wallet.KeyManager.GetKeys(KeyState.Clean).Count();
		GeneratedLockedKeyCount = _wallet.KeyManager.GetKeys(KeyState.Locked).Count();
		GeneratedUsedKeyCount = _wallet.KeyManager.GetKeys(KeyState.Used).Count();

		LargestExternalKeyGap = _wallet.KeyManager.CountConsecutiveUnusedKeys(isInternal: false, ignoreTail: true);
		LargestInternalKeyGap = _wallet.KeyManager.CountConsecutiveUnusedKeys(isInternal: true, ignoreTail: true);
	}
}
