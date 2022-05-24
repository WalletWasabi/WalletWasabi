using ReactiveUI;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletViewModel : WalletViewModelBase
{
	private readonly ObservableAsPropertyHelper<bool> _isMusicBoxVisible;

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
		WalletDashboard = new WalletDashboardViewModel(this, balanceChanged);

		balanceChanged
			.Subscribe(_ => IsWalletBalanceZero = wallet.Coins.TotalAmount() == Money.Zero)
			.DisposeWith(Disposables);

		if (Services.HostedServices.GetOrDefault<CoinJoinManager>() is { } coinJoinManager)
		{
			static bool? MaybeCoinjoining(StatusChangedEventArgs args) =>
				args switch
				{
					StartedEventArgs _ => true,
					StoppedEventArgs _ => false,
					CompletedEventArgs => false,
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

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!wallet.KeyManager.IsWatchOnly || wallet.KeyManager.IsHardwareWallet));

		_isMusicBoxVisible =
			this.WhenAnyValue(x => x.IsSelected, x => x.IsWalletBalanceZero)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero) = tuple;
					return isSelected && !isWalletBalanceZero && !wallet.KeyManager.IsWatchOnly;
				})
				.ToProperty(this, x => x.IsMusicBoxVisible);

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

	public bool IsMusicBoxVisible => _isMusicBoxVisible.Value;

	internal CoinJoinStateViewModel CoinJoinStateViewModel { get; }

	public WalletSettingsViewModel Settings { get; }

	public ICommand SendCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; }

	public ICommand WalletInfoCommand { get; }

	public ICommand WalletSettingsCommand { get; }

	public ICommand WalletStatisticsCommand { get; }

	public ICommand WalletCoinsCommand { get; }

	private CompositeDisposable Disposables { get; }

	public HistoryViewModel History { get; }

	public WalletDashboardViewModel WalletDashboard { get; }

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

		History.Activate(disposables);
		WalletDashboard.Activate(disposables);
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
