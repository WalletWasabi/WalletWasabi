using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

[NavigationMetaData(
	Title = "Wallet Coins",
	Caption = "Display wallet coins",
	IconName = "nav_wallet_24_regular",
	Order = 0,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Coins", "UTXO", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private IObservable<bool> _isAnySelected = Observable.Return(false);

	[AutoNotify]
	private FlatTreeDataGridSource<WalletCoinViewModel> _source = new(Enumerable.Empty<WalletCoinViewModel>());

	private WalletCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;
		SkipCommand = ReactiveCommand.CreateFromTask(OnSendCoinsAsync);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var coinChanges =
			_wallet.Coins.List
				.Connect()
				.TransformWithInlineUpdate(x => new WalletCoinViewModel(x), (_, _) => { })
				.Replay(1)
				.RefCount();

		IsAnySelected =
			coinChanges
				.AutoRefresh(x => x.IsSelected)
				.ToCollection()
				.Select(items => items.Any(t => t.IsSelected))
				.ObserveOn(RxApp.MainThreadScheduler);

		coinChanges
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var coinsCollection)
			.Subscribe()
			.DisposeWith(disposables);

		coinChanges
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(c => coinsCollection.Where(x => x.Model.IsSameAddress(c.Sender.Model) && x.IsSelected != c.Sender.IsSelected))
			.Do(coins =>
			{
				// Select/deselect all the coins on the same address.
				foreach (var coin in coins)
				{
					coin.IsSelected = !coin.IsSelected;
				}
			})
			.Subscribe()
			.DisposeWith(disposables);

		Source = TdgSourceFactory.CreateGridSource(coinsCollection)
			.DisposeWith(disposables);

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
			IsFixedAmount = true,
		};

		// TODO: Remove this after TransactionPreviewViewModel is decoupled.
		var wallet = MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _wallet.Name).WalletViewModel;
		Navigate().To().TransactionPreview(wallet, info);
	}
}
