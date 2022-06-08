using System.Collections.Immutable;
using DynamicData;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData.Binding;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

[NavigationMetaData(Title = "Wallet Coins (UTXOs)")]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly WalletViewModel _walletViewModel;
	private readonly IObservable<Unit> _balanceChanged;
	private readonly ObservableCollectionExtended<WalletCoinViewModel> _coins;
	private readonly SourceList<WalletCoinViewModel> _coinsSourceList = new();
	[AutoNotify] private FlatTreeDataGridSource<WalletCoinViewModel>? _source;
	[AutoNotify] private bool _anySelected;

	public WalletCoinsViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		SetupCancel(false, true, true);
		NextCommand = CancelCommand;
		_walletViewModel = walletViewModel;
		_balanceChanged = balanceChanged;
		_coins = new ObservableCollectionExtended<WalletCoinViewModel>();

		SkipCommand = ReactiveCommand.CreateFromTask(OnSendCoins);
	}

	private async Task OnSendCoins()
	{
		var wallet = _walletViewModel.Wallet;
		var selectedSmartCoins = _coins.Where(x => x.IsSelected).Select(x => x.Coin).ToImmutableArray();
		var info = new TransactionInfo(wallet.KeyManager.AnonScoreTarget);

		var addressDialog = new AddressEntryDialogViewModel(wallet.Network, info);
		var addressResult = await NavigateDialogAsync(addressDialog, NavigationTarget.CompactDialogScreen);
		if (addressResult.Result is not { } address)
		{
			return;
		}

		var labelDialog = new LabelEntryDialogViewModel(wallet, info);
		var result = await NavigateDialogAsync(labelDialog, NavigationTarget.CompactDialogScreen);
		if (result.Result is not { } label)
		{
			return;
		}

		info.Coins = selectedSmartCoins;
		info.Amount = selectedSmartCoins.Sum(x => x.Amount);
		info.SubtractFee = true;
		info.UserLabels = label;
		info.IsSelectedCoinModificationEnabled = false;

		Navigate().To(new TransactionPreviewViewModel(wallet, info, address, isFixedAmount: true));
	}

	private IObservable<Unit> CoinsUpdated => _balanceChanged
		.ToSignal()
		.Merge(_walletViewModel
			.WhenAnyValue(w => w.IsCoinJoining)
			.ToSignal());

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_coinsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(_coins)
			.DisposeMany()
			.Subscribe()
			.DisposeWith(disposables);

		_coinsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.WhenValueChanged(x => x.IsSelected)
			.Select(_ => _coinsSourceList.Items.Any(x => x.IsSelected))
			.Subscribe(anySelected => AnySelected = anySelected)
			.DisposeWith(disposables);

		Observable.Timer(TimeSpan.FromSeconds(30))
			.Subscribe(_ =>
			{
				foreach (var coin in GetCoins())
				{
					coin.RefreshAndGetIsBanned();
				}
			})
			.DisposeWith(disposables);

		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Selection		SelectionColumnView		-			Auto		-				-			false
		// Indicators		IndicatorsColumnView	-			Auto		-				-			true
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		// AnonymityScore	AnonymityColumnView		<custom>	50			-				-			true
		// Labels			LabelsColumnView		Labels		*			-				-			true

		Source = new FlatTreeDataGridSource<WalletCoinViewModel>(_coins)
		{
			Columns =
			{
				// Selection
				new TemplateColumn<WalletCoinViewModel>(
					null,
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new SelectionColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// Indicators
				new TemplateColumn<WalletCoinViewModel>(
					null,
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new IndicatorsColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = WalletCoinViewModel.SortAscending(x => x.CoinJoinInProgress),
						CompareDescending = WalletCoinViewModel.SortDescending(x => x.CoinJoinInProgress),
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// Amount
				new TemplateColumn<WalletCoinViewModel>(
					"Amount",
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AmountColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = WalletCoinViewModel.SortAscending(x => x.Amount),
						CompareDescending = WalletCoinViewModel.SortDescending(x => x.Amount)
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// AnonymityScore
				new TemplateColumn<WalletCoinViewModel>(
					new AnonymitySetHeaderView(),
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AnonymitySetColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = WalletCoinViewModel.SortAscending(x => x.AnonymitySet),
						CompareDescending = WalletCoinViewModel.SortDescending(x => x.AnonymitySet)
					},
					width: new GridLength(50, GridUnitType.Pixel)),

				// Labels
				new TemplateColumn<WalletCoinViewModel>(
					"Labels",
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new LabelsColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = WalletCoinViewModel.SortAscending(x => x.SmartLabel),
						CompareDescending = WalletCoinViewModel.SortDescending(x => x.SmartLabel),
					},
					width: new GridLength(1, GridUnitType.Star)),
			}
		};

		disposables.Add(Disposable.Create(() => _coins.Clear()));

		Source.DisposeWith(disposables);

		Source.RowSelection!.SingleSelect = true;

		CoinsUpdated
			.Select(_ => GetCoins())
			.Subscribe(RefreshCoinsList)
			.DisposeWith(disposables);
	}

	private ICoinsView GetCoins()
	{
		return _walletViewModel.Wallet.Coins;
	}

	private void RefreshCoinsList(ICoinsView items)
	{
		_coinsSourceList.Edit(x =>
		{
			x.Clear();
			x.AddRange(items.Select(coin => new WalletCoinViewModel(coin)));
		});
	}
}
