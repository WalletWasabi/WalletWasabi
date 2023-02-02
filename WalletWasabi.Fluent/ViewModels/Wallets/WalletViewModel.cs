using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletViewModel : RoutableViewModel, IComparable<WalletViewModel>
{
	private readonly NavBarWalletStateViewModel _parent;
	[AutoNotify] private double _widthSource;
	[AutoNotify] private double _heightSource;
	[AutoNotify] private bool _isPointerOver;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isWalletBalanceZero;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isTransactionHistoryEmpty;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isSendButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private bool _isLoading;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private bool _isCoinJoining;

	public WalletState WalletState => Wallet.State;

	private string _title;


	public Wallet Wallet { get; }

	public string WalletName => Wallet.WalletName;

	public bool IsLoggedIn => Wallet.IsLoggedIn;

	public bool PreferPsbtWorkflow => Wallet.KeyManager.PreferPsbtWorkflow;

	public int CompareTo(WalletViewModel? other)
	{
		if (other is null)
		{
			return -1;
		}

		var result = other.IsLoggedIn.CompareTo(IsLoggedIn);

		if (result == 0)
		{
			result = string.Compare(Title, other.Title, StringComparison.Ordinal);
		}

		return result;
	}

	public override string ToString() => WalletName;

	protected WalletViewModel(NavBarWalletStateViewModel parent)
	{
		_parent = parent;

		Wallet = parent.Wallet;
	}

	private bool _isInitialized;

	public UiTriggers UiTriggers { get; private set; }

	public CoinJoinSettingsViewModel CoinJoinSettings { get; private set; }

	public bool IsWatchOnly => Wallet.KeyManager.IsWatchOnly;


	internal CoinJoinStateViewModel CoinJoinStateViewModel { get; private set; }

	public WalletSettingsViewModel Settings { get; private set; }

	public ICommand SendCommand { get; private set; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; private set; }

	public ICommand WalletInfoCommand { get; private set; }

	public ICommand WalletSettingsCommand { get; private set; }

	public ICommand WalletStatsCommand { get; private set; }

	public ICommand WalletCoinsCommand { get; private set; }

	public ICommand CoinJoinSettingsCommand { get; private set; }

	private CompositeDisposable Disposables { get; set; }

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HistoryViewModel _history;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private IEnumerable<ActivatableViewModel> _tiles;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private IObservable<bool> _isMusicBoxVisible;

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
		if (!_isInitialized)
		{
			InitializeWallet();
			_isInitialized = true;
		}

		History.Activate(disposables);

		foreach (var tile in Tiles)
		{
			tile.Activate(disposables);
		}

		Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => this.RaisePropertyChanged(nameof(WalletState)))
			.DisposeWith(disposables);
	}

	private void InitializeWallet()
	{
		_title = WalletName;

		SetIcon();

		this.WhenAnyValue(x => x.IsCoinJoining)
			.Skip(1)
			.Subscribe(_ => MainViewModel.Instance.InvalidateIsCoinJoinActive());

		Disposables = Disposables is null
			? new CompositeDisposable()
			: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

		Settings = new WalletSettingsViewModel(this);
		CoinJoinSettings = new CoinJoinSettingsViewModel(this);
		UiTriggers = new UiTriggers(this);
		History = new HistoryViewModel(this);

		UiTriggers.TransactionsUpdateTrigger
			.Subscribe(_ => IsWalletBalanceZero = Wallet.Coins.TotalAmount() == Money.Zero)
			.DisposeWith(Disposables);

		if (Services.HostedServices.GetOrDefault<CoinJoinManager>() is { } coinJoinManager)
		{
			static bool? MaybeCoinjoining(StatusChangedEventArgs args) =>
				args switch
				{
					CoinJoinStatusEventArgs e when e.CoinJoinProgressEventArgs is EnteringInputRegistrationPhase =>
						true,
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
			.Subscribe(x => IsTransactionHistoryEmpty = x);

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero &&
			                                      (!Wallet.KeyManager.IsWatchOnly ||
			                                       Wallet.KeyManager.IsHardwareWallet));

		IsMusicBoxVisible =
			this.WhenAnyValue(x => x._parent.IsSelected, x => x.IsWalletBalanceZero, x => x.CoinJoinStateViewModel.AreAllCoinsPrivate, x => x.IsPointerOver)
				.Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero, areAllCoinsPrivate, pointerOver) = tuple;
					return (isSelected && !isWalletBalanceZero && (!areAllCoinsPrivate || pointerOver)) && !Wallet.KeyManager.IsWatchOnly;
				});

		SendCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new SendViewModel(this)));

		ReceiveCommand =
			ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new ReceiveViewModel(Wallet)));

		WalletInfoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!string.IsNullOrEmpty(Wallet.Kitchen.SaltSoup()))
			{
				var pwAuthDialog = new PasswordAuthDialogViewModel(Wallet);
				var dialogResult = await NavigateDialogAsync(pwAuthDialog, NavigationTarget.CompactDialogScreen);

				if (!dialogResult.Result)
				{
					return;
				}
			}

			Navigate(NavigationTarget.DialogScreen).To(new WalletInfoViewModel(this));
		});

		WalletStatsCommand =
			ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new WalletStatsViewModel(this)));

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(Settings));

		WalletCoinsCommand =
			ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new WalletCoinsViewModel(this)));

		CoinJoinSettingsCommand =
			ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(CoinJoinSettings),
				Observable.Return(!Wallet.KeyManager.IsWatchOnly));

		CoinJoinStateViewModel = new CoinJoinStateViewModel(this);

		Tiles = GetTiles().ToList();
	}

	public static WalletViewModel Create(NavBarWalletStateViewModel parent)
	{
		return parent.Wallet.KeyManager.IsHardwareWallet
			? new HardwareWalletViewModel(parent)
			: parent.Wallet.KeyManager.IsWatchOnly
				? new WatchOnlyWalletViewModel(parent)
				: new WalletViewModel(parent);
	}

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	private IEnumerable<ActivatableViewModel> GetTiles()
	{
		yield return new WalletBalanceTileViewModel(this);

		if (!IsWatchOnly)
		{
			yield return new PrivacyControlTileViewModel(this);
		}

		yield return new BtcPriceTileViewModel(Wallet);
	}


	private void SetIcon()
	{
		var walletType = WalletHelpers.GetType(Wallet.KeyManager);

		var baseResourceName = walletType switch
		{
			WalletType.Coldcard => "coldcard_24",
			WalletType.Trezor => "trezor_24",
			WalletType.Ledger => "ledger_24",
			_ => "wallet_24"
		};

		IconName = $"nav_{baseResourceName}_regular";
		IconNameFocused = $"nav_{baseResourceName}_filled";
	}
}
