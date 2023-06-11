using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
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
	private readonly WalletPageViewModel _parent;
	[AutoNotify] private double _widthSource;
	[AutoNotify] private double _heightSource;
	[AutoNotify] private bool _isPointerOver;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isTransactionHistoryEmpty;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private bool _isLoading;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private bool _isCoinJoining;

	public WalletViewModel(UiContext uiContext, WalletPageViewModel parent)
	{
		_parent = parent;
		Wallet = parent.Wallet;
		UiContext = uiContext;

		_title = WalletName;

		this.WhenAnyValue(x => x.IsCoinJoining)
			.Skip(1)
			.Subscribe(_ => MainViewModel.Instance.InvalidateIsCoinJoinActive());

		Disposables = Disposables is null
			? new CompositeDisposable()
			: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

		//TODO: remove this after ConfirmRecoveryWordsViewModel is decoupled
		var walletModel = new WalletModel(Wallet);

		Settings = new WalletSettingsViewModel(UiContext, walletModel);
		CoinJoinSettings = new CoinJoinSettingsViewModel(UiContext, walletModel);
		UiTriggers = new UiTriggers(this);
		History = new HistoryViewModel(UiContext, this);

		UiTriggers.TransactionsUpdateTrigger
			.Subscribe(_ => IsWalletBalanceZero = Wallet.Coins.TotalAmount() == Money.Zero)
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
			.Subscribe(x => IsTransactionHistoryEmpty = x);

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!Wallet.KeyManager.IsWatchOnly || Wallet.KeyManager.IsHardwareWallet));

		IsMusicBoxVisible =
			this.WhenAnyValue(x => x._parent.IsSelected, x => x.IsWalletBalanceZero, x => x.CoinJoinStateViewModel.AreAllCoinsPrivate, x => x.IsPointerOver)
				.Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero, areAllCoinsPrivate, pointerOver) = tuple;
					return (isSelected && !isWalletBalanceZero && (!areAllCoinsPrivate || pointerOver)) && !Wallet.KeyManager.IsWatchOnly;
				});

		SendCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new SendViewModel(UiContext, this)));

		ReceiveCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To().Receive(new WalletModel(Wallet)));

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

			Navigate().To().WalletInfo(this);
		});

		WalletStatsCommand = ReactiveCommand.Create(() => Navigate().To().WalletStats(this));

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(Settings));

		WalletCoinsCommand = ReactiveCommand.Create(() => Navigate().To().WalletCoins(this));

		CoinJoinSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(CoinJoinSettings), Observable.Return(!Wallet.KeyManager.IsWatchOnly));

		CoinJoinStateViewModel = new CoinJoinStateViewModel(UiContext, this);

		Tiles = GetTiles().ToList();

		this.WhenAnyValue(x => x.Settings.PreferPsbtWorkflow)
			.Do(x => this.RaisePropertyChanged(nameof(PreferPsbtWorkflow)))
			.Subscribe();
	}

	public WalletState WalletState => Wallet.State;

	private string _title;

	public Wallet Wallet { get; }

	public string WalletName => Wallet.WalletName;

	public bool IsLoggedIn => Wallet.IsLoggedIn;

	public bool PreferPsbtWorkflow => Wallet.KeyManager.PreferPsbtWorkflow;

	public override string ToString() => WalletName;

	public UiTriggers UiTriggers { get; private set; }

	public CoinJoinSettingsViewModel CoinJoinSettings { get; private set; }

	public bool IsWatchOnly => Wallet.KeyManager.IsWatchOnly;

	public IObservable<bool> IsMusicBoxVisible { get; }

	internal CoinJoinStateViewModel CoinJoinStateViewModel { get; private set; }

	public WalletSettingsViewModel Settings { get; private set; }

	public HistoryViewModel History { get; }

	public IEnumerable<ActivatableViewModel> Tiles { get; }

	public ICommand SendCommand { get; private set; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; private set; }

	public ICommand WalletInfoCommand { get; private set; }

	public ICommand WalletSettingsCommand { get; private set; }

	public ICommand WalletStatsCommand { get; private set; }

	public ICommand WalletCoinsCommand { get; private set; }

	public ICommand CoinJoinSettingsCommand { get; private set; }

	private CompositeDisposable Disposables { get; set; }

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

	public static WalletViewModel Create(UiContext uiContext, WalletPageViewModel parent)
	{
		return parent.Wallet.KeyManager.IsHardwareWallet
			? new HardwareWalletViewModel(uiContext, parent)
			: parent.Wallet.KeyManager.IsWatchOnly
				? new WatchOnlyWalletViewModel(uiContext, parent)
				: new WalletViewModel(uiContext, parent);
	}

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	private IEnumerable<ActivatableViewModel> GetTiles()
	{
		var walletModel = new WalletModel(Wallet);
		var balances = walletModel.Balances;

		yield return new WalletBalanceTileViewModel(balances);

		if (!IsWatchOnly)
		{
			yield return new PrivacyControlTileViewModel(UiContext, this);
		}

		yield return new BtcPriceTileViewModel(balances);
	}

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
}
