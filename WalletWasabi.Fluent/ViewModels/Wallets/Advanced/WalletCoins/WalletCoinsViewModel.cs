using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.CoinControl;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

[NavigationMetaData(
	Title = "Wallet Coins (UTXOs)",
	Caption = "Display wallet coins",
	IconName = "nav_wallet_24_regular",
	Order = 0,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Coins", "UTXO" },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private CoinSelectorViewModel? _coinSelector;

	[AutoNotify] private IObservable<bool> _isAnySelected = Observable.Return(false);

	[AutoNotify] private FlatTreeDataGridSource<WalletCoinViewModel> _source = new(Enumerable.Empty<WalletCoinViewModel>());

	private WalletCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
		SkipCommand = ReactiveCommand.CreateFromTask(OnSendCoinsAsync);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		CoinSelector = new CoinSelectorViewModel(_wallet, new List<ICoinModel>());
		CoinSelector.DisposeWith(disposables);

		IsAnySelected = CoinSelector.Selection.ToObservableChangeSet().Count().Select(i => i > 0);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnSendCoinsAsync()
	{
		// TODO: Leaky abstraction. SmartCoin shouldn't be exposed here.
		// What we need is a TransactionInfo that can operate with ICoinModel instead.
		var selectedSmartCoins =
			Source.Items
				.Where(x => x.IsSelected)
				.Select(x => x.Model.GetSmartCoin())
				.ToImmutableArray();

		var addressResult = await Navigate().To().AddressEntryDialog(_wallet.Network).GetResultAsync();
		if (addressResult is not { } address || address.Address is null)
		{
			return;
		}

		var labelsResult = await Navigate().To().LabelEntryDialog(_wallet, address.Label ?? LabelsArray.Empty).GetResultAsync();
		if (labelsResult is not { } label)
		{
			return;
		}

		var info = new TransactionInfo(address.Address, _wallet.Settings.AnonScoreTarget)
		{
			Coins = selectedSmartCoins,
			Amount = selectedSmartCoins.Sum(x => x.Amount),
			SubtractFee = true,
			Recipient = label,
			IsSelectedCoinModificationEnabled = false,
			IsFixedAmount = true
		};

		// TODO: Remove this after TransactionPreviewViewModel is decoupled.
		var wallet = MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _wallet.Name).WalletViewModel;
		Navigate().To().TransactionPreview(wallet, info);
	}
}
