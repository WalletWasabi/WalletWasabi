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
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

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

	protected WalletViewModel(Wallet wallet) : base(wallet)
	{
		Disposables = Disposables is null
			? new CompositeDisposable()
			: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

		var balanceChanged =
			Observable.FromEventPattern(
					Wallet.TransactionProcessor,
					nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.Select(_ => Unit.Default)
				.Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFilterProcessed))
					.Select(_ => Unit.Default))
				.Merge(Services.UiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.ObserveOn(RxApp.MainThreadScheduler);

		History = new HistoryViewModel(this, balanceChanged);

		Settings = new WalletSettingsViewModel(this);

		balanceChanged
			.Subscribe(_ => IsWalletBalanceZero = wallet.Coins.TotalAmount() == Money.Zero)
			.DisposeWith(Disposables);

		if (Services.HostedServices.GetOrDefault<CoinJoinManager>() is { } coinJoinManager)
		{
			Observable
				.FromEventPattern<WalletStatusChangedEventArgs>(coinJoinManager, nameof(CoinJoinManager.WalletStatusChanged))
				.Select(args => args.EventArgs)
				.Where(e => e.Wallet == Wallet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e => IsCoinJoining = e.IsCoinJoining)
				.DisposeWith(Disposables);
		}

		this.WhenAnyValue(x => x.History.IsTransactionHistoryEmpty)
			.Subscribe(x => IsEmptyWallet = x);

		_smallLayoutHeightBreakpoint = 650;
		_wideLayoutWidthBreakpoint = 1400;

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

		SendCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen)
				.To(new SendViewModel(wallet)));

		ReceiveCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen)
				.To(new ReceiveViewModel(wallet)));

		WalletInfoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!string.IsNullOrEmpty(wallet.Kitchen.SaltSoup()))
			{
				var pwAuthDialog = new PasswordAuthDialogViewModel(wallet);
				var res = await NavigateDialogAsync(pwAuthDialog, NavigationTarget.CompactDialogScreen);

				if (!res.Result && res.Kind == DialogResultKind.Normal)
				{
					await ShowErrorAsync("Wallet Info", "The password is incorrect! Try Again.", "");
					return;
				}
				else if (res.Kind is DialogResultKind.Back or DialogResultKind.Cancel)
				{
					return;
				}
			}

			Navigate(NavigationTarget.DialogScreen).To(new WalletInfoViewModel(this));
		});

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(Settings));
	}

	public WalletSettingsViewModel Settings { get; }

	public ICommand SendCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; }

	public ICommand WalletInfoCommand { get; }

	public ICommand WalletSettingsCommand { get; }

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
