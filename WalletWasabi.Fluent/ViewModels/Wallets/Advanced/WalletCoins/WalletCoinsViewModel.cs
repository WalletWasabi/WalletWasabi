using DynamicData;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData.Binding;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

[NavigationMetaData(Title = "Wallet Coins")]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly WalletViewModel _walletViewModel;
	private readonly IObservable<Unit> _balanceChanged;
	private readonly ObservableCollectionExtended<WalletCoinViewModel> _coins;
	private readonly SourceList<WalletCoinViewModel> _coinsSourceList = new();

	public WalletCoinsViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		SetupCancel(false, true, true);
		NextCommand = CancelCommand;
		_walletViewModel = walletViewModel;
		_balanceChanged = balanceChanged;
		_coins = new ObservableCollectionExtended<WalletCoinViewModel>();

		_coinsSourceList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(_coins)
			.Subscribe();

		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Indicators		IndicatorsColumnView	-			Auto		-				-			false
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		// AnonymitySet		AnonymityColumnView		<custom>	40			-				-			true
		// Labels			LabelsColumnView		Labels		*			-				-			false

		Source = new FlatTreeDataGridSource<WalletCoinViewModel>(_coins)
		{
			Columns =
			{
				// Indicators
				new TemplateColumn<WalletCoinViewModel>(
					null,
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new IndicatorsColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// Amount
				new TemplateColumn<WalletCoinViewModel>(
					new AnonymitySetHeaderView(),
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AmountColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = WalletCoinViewModel.SortAscending(x => x.Amount),
						CompareDescending = WalletCoinViewModel.SortDescending(x => x.Amount)
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// AnonymitySet
				new TemplateColumn<WalletCoinViewModel>(
					"AnonymitySet",
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AnonymitySetColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = WalletCoinViewModel.SortAscending(x => x.AnonymitySet),
						CompareDescending = WalletCoinViewModel.SortDescending(x => x.AnonymitySet)
					},
					width: new GridLength(40, GridUnitType.Pixel)),

				// Labels
				new TemplateColumn<WalletCoinViewModel>(
					"Labels",
					new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new LabelsColumnView(), true),
					options: new ColumnOptions<WalletCoinViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false
					},
					width: new GridLength(1, GridUnitType.Star))
			}
		};

		Source.RowSelection!.SingleSelect = true;

	}

	public FlatTreeDataGridSource<WalletCoinViewModel> Source { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.Merge(
			_balanceChanged.Select(_ => Unit.Default),
			_walletViewModel.WhenAnyValue(w => w.IsCoinJoining).Select(_ => Unit.Default))
			.Subscribe(_ =>
			{
				Update();
			});

		disposables.Add(_coinsSourceList);
	}

	private void Update()
	{
		var coins = _walletViewModel.Wallet.Coins.Select(c => new WalletCoinViewModel(c));

		_coinsSourceList.Edit(x =>
		{
			x.Clear();
			x.AddRange(coins);
		});
	}
}
