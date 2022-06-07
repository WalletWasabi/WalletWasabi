using ReactiveUI;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using System.Windows.Input;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletViewModel : WalletViewModelBase
{
	private readonly double _smallLayoutHeightBreakpoint;
	private readonly double _wideLayoutWidthBreakpoint;
	private readonly int _smallLayoutIndex;
	private readonly int _normalLayoutIndex;
	private readonly int _wideLayoutIndex;
	[AutoNotify] private IList<TileViewModel> _tiles;
	[AutoNotify] private IList<TileLayoutViewModel>? _layouts;
	[AutoNotify] private int _layoutIndex;
	[AutoNotify] private double _widthSource;
	[AutoNotify] private double _heightSource;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSmallLayout;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isNormalLayout;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWideLayout;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isEmptyWallet;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	protected WalletViewModel(Wallet wallet) : base(wallet)
	{
		Disposables = Disposables is null
			? new CompositeDisposable()
			: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

		Settings = new WalletSettingsViewModel(this);

		var balanceChanged =
			Observable.FromEventPattern(
					Wallet.TransactionProcessor,
					nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.Select(_ => Unit.Default)
				.Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFilterProcessed))
					.Select(_ => Unit.Default))
				.Merge(Services.UiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.Merge(Settings.WhenAnyValue(x => x.AnonScoreTarget).Select(_ => Unit.Default).Skip(1).Throttle(TimeSpan.FromMilliseconds(3000))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.ObserveOn(RxApp.MainThreadScheduler));

		History = new HistoryViewModel(this, balanceChanged);

		balanceChanged
			.Subscribe(_ => IsWalletBalanceZero = wallet.Coins.TotalAmount() == Money.Zero)
			.DisposeWith(Disposables);

		if (Services.HostedServices.GetOrDefault<CoinJoinManager>() is { } coinJoinManager)
		{
			static bool? MaybeCoinjoining(StatusChangedEventArgs args) =>
				args switch
				{
					CoinJoinStatusEventArgs e when e.CoinJoinProgressEventArgs is EnteringInputRegistrationPhase => true,
					CompletedEventArgs _ => false,
					_ => null
				};

			Observable
				.FromEventPattern<StatusChangedEventArgs>(coinJoinManager, nameof(CoinJoinManager.StatusChanged))
				.Select(args => args.EventArgs)
				.Where(e => e.Wallet == Wallet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e => IsCoinJoining = MaybeCoinjoining(e) ?? IsCoinJoining)
				.DisposeWith(Disposables);
		}

		this.WhenAnyValue(x => x.History.IsTransactionHistoryEmpty)
			.Subscribe(x => IsEmptyWallet = x);

		_smallLayoutHeightBreakpoint = double.MaxValue;
		_wideLayoutWidthBreakpoint = double.MaxValue;

		_smallLayoutIndex = 0;
		_normalLayoutIndex = 1;
		_wideLayoutIndex = 2;

		Layouts = wallet.KeyManager.IsWatchOnly
			? TileHelper.GetWatchOnlyWalletLayout()
			: TileHelper.GetNormalWalletLayout();

		LayoutIndex = _normalLayoutIndex;

		_tiles = wallet.KeyManager.IsWatchOnly
			? TileHelper.GetWatchOnlyWalletTiles(this, balanceChanged)
			: TileHelper.GetNormalWalletTiles(this, balanceChanged);

		this.WhenAnyValue(x => x.LayoutIndex)
			.Subscribe(x =>
			{
				SetLayoutFlag(x);
				NotifyLayoutChanged();
				UpdateTiles();
			});

		this.WhenAnyValue(x => x.WidthSource)
			.Subscribe(x => LayoutSelector(x, _heightSource));

		this.WhenAnyValue(x => x.HeightSource)
			.Subscribe(x => LayoutSelector(_widthSource, x));

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!wallet.KeyManager.IsWatchOnly || wallet.KeyManager.IsHardwareWallet));

		IsMusicBoxVisible =
			this.WhenAnyValue(x => x.IsSelected, x => x.IsWalletBalanceZero)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero) = tuple;
					return isSelected && !isWalletBalanceZero && !wallet.KeyManager.IsWatchOnly;
				});

		SendCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new SendViewModel(wallet, balanceChanged, History.UnfilteredTransactions)));

		ReceiveCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new ReceiveViewModel(wallet)));

		WalletInfoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!string.IsNullOrEmpty(wallet.Kitchen.SaltSoup()))
			{
				var pwAuthDialog = new PasswordAuthDialogViewModel(wallet);
				var dialogResult = await NavigateDialogAsync(pwAuthDialog, NavigationTarget.CompactDialogScreen);

				if (!dialogResult.Result)
				{
					return;
				}
			}

			Navigate(NavigationTarget.DialogScreen).To(new WalletInfoViewModel(this));
		});

		WalletStatisticsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new WalletStatsViewModel(this)));

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(Settings));

		WalletCoinsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new WalletCoinsViewModel(this, balanceChanged)));

		CoinJoinStateViewModel = new CoinJoinStateViewModel(this, balanceChanged);
	}

	public IObservable<bool> IsMusicBoxVisible { get; }

	internal CoinJoinStateViewModel CoinJoinStateViewModel { get; }

	public WalletSettingsViewModel Settings { get; }

	public ICommand SendCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; }

	public ICommand WalletInfoCommand { get; }

	public ICommand WalletSettingsCommand { get; }

	public ICommand WalletStatisticsCommand { get; }

	public ICommand WalletCoinsCommand { get; }

	private CompositeDisposable Disposables { get; set; }

	public HistoryViewModel History { get; }

	public TileLayoutViewModel? CurrentLayout => Layouts?[LayoutIndex];

	private void LayoutSelector(double width, double height)
	{
		if (height < _smallLayoutHeightBreakpoint)
		{
			// Small Layout
			LayoutIndex = _smallLayoutIndex;
		}
		else
		{
			if (width < _wideLayoutWidthBreakpoint)
			{
				// Normal Layout
				LayoutIndex = _normalLayoutIndex;
			}
			else
			{
				// Wide Layout
				LayoutIndex = _wideLayoutIndex;
			}
		}
	}

	private void NotifyLayoutChanged()
	{
		this.RaisePropertyChanged(nameof(CurrentLayout));
	}

	private void UpdateTiles()
	{
		if (Tiles != null)
		{
			foreach (var tile in Tiles)
			{
				tile.TilePresetIndex = LayoutIndex;
			}
		}
	}

	private void SetLayoutFlag(int layoutIndex)
	{
		IsSmallLayout = layoutIndex == _smallLayoutIndex;
		IsNormalLayout = layoutIndex == _normalLayoutIndex;
		IsWideLayout = layoutIndex == _wideLayoutIndex;
	}

	public void NavigateAndHighlight(uint256 txid)
	{
		Navigate().To(this, NavigationMode.Clear);

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			await Task.Delay(500);
			History.SelectTransaction(txid);
		});
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		foreach (var tile in _tiles)
		{
			tile.Activate(disposables);
		}

		History.Activate(disposables);
	}

	public static WalletViewModel Create(Wallet wallet)
	{
		return wallet.KeyManager.IsHardwareWallet
			? new HardwareWalletViewModel(wallet)
			: wallet.KeyManager.IsWatchOnly
				? new WatchOnlyWalletViewModel(wallet)
				: new WalletViewModel(wallet);
	}
}
