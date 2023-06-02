using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
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

public partial class WalletViewModel : WalletViewModelBase
{
	[AutoNotify] private double _widthSource;
	[AutoNotify] private double _heightSource;
	[AutoNotify] private bool _isPointerOver;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isTransactionHistoryEmpty;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	protected WalletViewModel(UiContext uiContext, Wallet wallet) : base(wallet)
	{
		UiContext = uiContext;
		Disposables = Disposables is null
			? new CompositeDisposable()
			: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

		Settings = new WalletSettingsViewModel(this);
		CoinJoinSettings = new CoinJoinSettingsViewModel(this);
		UiTriggers = new UiTriggers(this);
		History = new HistoryViewModel(uiContext, this);

		UiTriggers.TransactionsUpdateTrigger
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
			.Subscribe(x => IsTransactionHistoryEmpty = x);

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!wallet.KeyManager.IsWatchOnly || wallet.KeyManager.IsHardwareWallet));

		IsMusicBoxVisible =
			this.WhenAnyValue(x => x.IsSelected, x => x.IsWalletBalanceZero, x => x.CoinJoinStateViewModel.AreAllCoinsPrivate, x => x.IsPointerOver)
				.Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero, areAllCoinsPrivate, pointerOver) = tuple;
					return (isSelected && !isWalletBalanceZero && (!areAllCoinsPrivate || pointerOver)) && !wallet.KeyManager.IsWatchOnly;
				});

		SendCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new SendViewModel(UiContext, this)));

		ReceiveCommand = ReactiveCommand.Create(() => Navigate().To().Receive(wallet));

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

		WalletStatsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new WalletStatsViewModel(this)));

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(Settings));

		WalletCoinsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(new WalletCoinsViewModel(UiContext, this)));

		CoinJoinSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(CoinJoinSettings), Observable.Return(!wallet.KeyManager.IsWatchOnly));

		CoinJoinStateViewModel = new CoinJoinStateViewModel(UiContext, this);

		Tiles = GetTiles().ToList();
	}

	public IEnumerable<ActivatableViewModel> Tiles { get; }

	public UiTriggers UiTriggers { get; }

	public CoinJoinSettingsViewModel CoinJoinSettings { get; }

	public bool IsWatchOnly => Wallet.KeyManager.IsWatchOnly;

	public IObservable<bool> IsMusicBoxVisible { get; }

	internal CoinJoinStateViewModel CoinJoinStateViewModel { get; }

	public WalletSettingsViewModel Settings { get; }

	public ICommand SendCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand ReceiveCommand { get; }

	public ICommand WalletInfoCommand { get; }

	public ICommand WalletSettingsCommand { get; }

	public ICommand WalletStatsCommand { get; }

	public ICommand WalletCoinsCommand { get; }

	public ICommand CoinJoinSettingsCommand { get; }

	private CompositeDisposable Disposables { get; }

	public HistoryViewModel History { get; }

	public void NavigateAndHighlight(uint256 txid)
	{
		if (OpenCommand.CanExecute(default))
		{
			OpenCommand.Execute(default);
		}

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

		foreach (var tile in Tiles)
		{
			tile.Activate(disposables);
		}
	}

	public static WalletViewModel Create(UiContext uiContext, Wallet wallet)
	{
		if (wallet.KeyManager.IsHardwareWallet)
		{
			return new HardwareWalletViewModel(uiContext, wallet);
		}

		if (wallet.KeyManager.IsWatchOnly)
		{
			return new WatchOnlyWalletViewModel(uiContext, wallet);
		}

		return new WalletViewModel(uiContext, wallet);
	}

	private IEnumerable<ActivatableViewModel> GetTiles()
	{
		var rateProvider = new ObservableExchangeRateProvider(Wallet.Synchronizer);
		var walletModel = new WalletModel(Wallet);
		var balances = new WalletBalancesModel(walletModel, rateProvider);

		yield return new WalletBalanceTileViewModel(balances);

		if (!IsWatchOnly)
		{
			yield return new PrivacyControlTileViewModel(UiContext, this);
		}

		yield return new BtcPriceTileViewModel(balances);
	}
}
