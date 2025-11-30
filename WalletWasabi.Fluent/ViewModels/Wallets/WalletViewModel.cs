using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Settings;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using ScriptType = WalletWasabi.Fluent.Models.Wallets.ScriptType;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[AppLifetime]
public partial class WalletViewModel : RoutableViewModel, IWalletViewModel
{
	public static string FindCoordinatorLink { get; } = "https://docs.wasabiwallet.io/FAQ/FAQ-UseWasabi.html#how-do-i-find-a-coordinator";

	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isCoinJoining;

	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isLoading;
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isSelected;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isDonateButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _areAllCoinsPrivate;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _hasMusicBoxBeenDisplayed;
	[AutoNotify] private bool _isMusicBoxFlyoutDisplayed;

	[AutoNotify] private ICommand _defaultReceiveCommand;
	[AutoNotify] private ICommand _defaultSendCommand;

	// This proxy fixes a stack overflow bug in Avalonia
	public bool IsMusicBoxFlyoutOpenedProxy
	{
		get => IsMusicBoxFlyoutDisplayed;
		set => IsMusicBoxFlyoutDisplayed = value;
	}

	private string _title = "";
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _loaded;

	private UiConfig _uiConfig { get; }

	public WalletViewModel(UiContext uiContext, IWalletModel walletModel, Wallet wallet)
	{
		UiContext = uiContext;
		WalletModel = walletModel;
		Wallet = wallet;

		Settings = new WalletSettingsViewModel(UiContext, WalletModel);
		History = new HistoryViewModel(UiContext, WalletModel);

		_uiConfig = Services.UiConfig;

		var searchItems = CreateSearchItems();
		this.WhenAnyValue(x => x.IsSelected)
			.Do(shouldDisplay => UiContext.EditableSearchSource.Toggle(searchItems, shouldDisplay))
			.Subscribe();

		var sendSearchItem = CreateSendItem();
		this.WhenAnyValue(x => x.IsSendButtonVisible, x => x.IsSelected, (x, y) => x && y)
			.Do(shouldAdd => UiContext.EditableSearchSource.Toggle(sendSearchItem, shouldAdd))
			.Subscribe();

		var donateSearchItem = CreateDonateItem();
		this.WhenAnyValue(x => x.IsDonateButtonVisible, x => x.IsSelected, (x, y) => x && y)
			.Do(shouldAdd => UiContext.EditableSearchSource.Toggle(donateSearchItem, shouldAdd))
			.Subscribe();

		walletModel.HasBalance
			.Select(x => !x)
			.BindTo(this, x => x.IsWalletBalanceZero);

		walletModel.IsCoinjoinRunning
			.BindTo(this, x => x.IsCoinJoining);

		 this.WhenAnyValue(x => x.IsWalletBalanceZero)
		 	.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!WalletModel.IsWatchOnlyWallet || WalletModel.IsHardwareWallet));

		 this.WhenAnyValue(x => x.IsSendButtonVisible)
			 .Subscribe(_ => IsDonateButtonVisible = IsSendButtonVisible && WalletModel.Network == Network.Main);

		 WalletModel.Privacy.IsWalletPrivate
			 .BindTo(this, x => x.AreAllCoinsPrivate);

		 IsMusicBoxVisible = this.WhenAnyValue(
			 x => x.HasMusicBoxBeenDisplayed,
			 x => x.IsSelected,
			 x => x.IsWalletBalanceZero,
			 x => x.AreAllCoinsPrivate,
			 x => x.IsPointerOver,
			 x => x.IsMusicBoxFlyoutDisplayed,
			 (hasBeenDisplayed, isSelected, hasNoBalance, areAllCoinsPrivate, isPointerOver, isMusicBoxFlyoutDisplayed) =>
			 {
				 if (!hasBeenDisplayed)
				 {
					 if (!WalletModel.IsCoinJoinEnabled)
					 {
						 // If there is no coordinator configured and it's the first time, display MusicBox even without pointer over
						 Task.Run(() => DelaySwitchHasMusicBoxBeenDisplayedAsync(CancellationToken.None));
						 return isSelected && !WalletModel.IsCoinJoinEnabled;
					 }

					 HasMusicBoxBeenDisplayed = true;
				 }

				 if (!WalletModel.IsCoinJoinEnabled)
				 {
					 return isSelected && !WalletModel.IsCoinJoinEnabled && (isPointerOver || isMusicBoxFlyoutDisplayed);
				 }

				 return (isSelected && !hasNoBalance && (!areAllCoinsPrivate || (isPointerOver || isMusicBoxFlyoutDisplayed))) && !WalletModel.IsWatchOnlyWallet;
			 });


		SendCommand = ReactiveCommand.Create(() => Navigate().To().Send(walletModel, new SendFlowModel(wallet, walletModel)));
		DonateCommand = ReactiveCommand.Create(() => Navigate().To().Send(walletModel, new SendFlowModel(wallet, walletModel, donate: true)));
		SendManualControlCommand = ReactiveCommand.Create(() => Navigate().To().ManualControlDialog(walletModel, wallet));

		this.WhenAnyValue(x => x.Settings.DefaultSendWorkflow)
			.Subscribe(value => DefaultSendCommand = value == SendWorkflow.Automatic ? SendCommand : SendManualControlCommand);

		SegwitReceiveCommand = ReactiveCommand.Create(() => Navigate().To().Receive(WalletModel, ScriptType.SegWit));
		TaprootReceiveCommand = SeveralReceivingScriptTypes ?
			ReactiveCommand.Create(() => Navigate().To().Receive(WalletModel, ScriptType.Taproot)) :
			null;

		this.WhenAnyValue(x => x.Settings.DefaultReceiveScriptType)
			.Subscribe(value =>
				DefaultReceiveCommand = value == ScriptType.SegWit || TaprootReceiveCommand is null
					? SegwitReceiveCommand
					: TaprootReceiveCommand);

		WalletInfoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (await AuthorizeForPasswordAsync())
			{
				Navigate().To().WalletInfo(WalletModel);
			}
		});

		WalletStatsCommand = ReactiveCommand.Create(() => Navigate().To().WalletStats(WalletModel));

		WalletSettingsCommand = ReactiveCommand.Create(
			() =>
			{
				Settings.SelectedTab = 0;
				Navigate(NavigationTarget.DialogScreen).To(Settings);
			});

		CoinJoinSettingsCommand = ReactiveCommand.Create(
			() =>
			{
				Settings.SelectedTab = 1;
				Navigate(NavigationTarget.DialogScreen).To(Settings);
			});

		WalletCoinsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To().WalletCoins(WalletModel));

		CoinJoinStateViewModel = WalletModel.IsCoinJoinEnabled
			? new CoinJoinStateViewModel(uiContext, WalletModel, WalletModel.Coinjoin!, Settings)
			: null;

		NavigateToCoordinatorSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (UiContext.MainViewModel is { } mainViewModel)
			{
				await mainViewModel.SettingsPage.ActivateCoordinatorTab();
			}
		});

		CoordinatorHelpCommand = ReactiveCommand.CreateFromTask(() => UiContext.FileSystem.OpenBrowserAsync("https://docs.wasabiwallet.io/FAQ/FAQ-UseWasabi.html#how-do-i-find-a-coordinator"));

		NavigateToExcludedCoinsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().ExcludedCoins(WalletModel));

		Tiles = GetTiles().ToList();

		this.WhenAnyValue(x => x.Settings.PreferPsbtWorkflow)
			.Do(x => this.RaisePropertyChanged(nameof(PreferPsbtWorkflow)))
			.Subscribe();

		this.WhenAnyValue(x => x.WalletModel.Name).BindTo(this, x => x.Title);
	}

	// TODO: Remove this
	public Wallet Wallet { get; }

	public IWalletModel WalletModel { get; }

	public bool IsLoggedIn => WalletModel.Auth.IsLoggedIn;

	public bool PreferPsbtWorkflow => WalletModel.Settings.PreferPsbtWorkflow;

	public bool SeveralReceivingScriptTypes => WalletModel.SeveralReceivingScriptTypes;

	public bool IsWatchOnly => WalletModel.IsWatchOnlyWallet;

	public IObservable<bool> IsMusicBoxVisible { get; }

	public CoinJoinStateViewModel? CoinJoinStateViewModel { get; private set; }

	public WalletSettingsViewModel Settings { get; private set; }

	public HistoryViewModel History { get; }

	public IEnumerable<ActivatableViewModel> Tiles { get; }

	public ICommand SendCommand { get; private set; }
	public ICommand DonateCommand { get; private set; }

	public ICommand SendManualControlCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }
	public ICommand SegwitReceiveCommand { get; private set; }
	public ICommand? TaprootReceiveCommand { get; private set; }

	public ICommand WalletInfoCommand { get; private set; }

	public ICommand WalletSettingsCommand { get; private set; }

	public ICommand WalletStatsCommand { get; private set; }

	public ICommand WalletCoinsCommand { get; private set; }

	public ICommand CoinJoinSettingsCommand { get; private set; }

	public ICommand NavigateToCoordinatorSettingsCommand { get; }

	public ICommand CoordinatorHelpCommand { get; }

	public ICommand NavigateToExcludedCoinsCommand { get; }

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	public void SelectTransaction(uint256 txid)
	{
		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			await Task.Delay(500);
			History.SelectTransaction(txid);
		});
	}

	public void NavigateAndHighlight(uint256 txid)
	{
		Navigate().To(this, NavigationMode.Clear);

		SelectTransaction(txid);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		History.Activate(disposables);

		foreach (var tile in Tiles)
		{
			tile.Activate(disposables);
		}

		WalletModel.Loaded
			.BindTo(this, x => x.Loaded)
			.DisposeWith(disposables);
	}

	private ISearchItem[] CreateSearchItems()
	{
		return new ISearchItem[]
		{
			new ActionableItem("Receive", "Display wallet receive dialog", () => { DefaultReceiveCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Receive", "Action", }) { Icon = "wallet_action_receive", IsDefault = true, Priority = 2 },
			new ActionableItem("Coinjoin Settings", "Display wallet coinjoin settings", () => { CoinJoinSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Settings", }) { Icon = "wallet_action_coinjoin", IsDefault = true, Priority = 3 },
			new ActionableItem("Wallet Settings", "Display wallet settings", () => { WalletSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Settings", }) { Icon = "settings_wallet_regular", IsDefault = true, Priority = 4 },
			new ActionableItem("Exclude Coins", "Display exclude coins", () => { NavigateToExcludedCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Exclude", "Coins", "Coinjoin", "Freeze", "UTXO", }) { Icon = "exclude_coins", IsDefault = true, Priority = 5 },
			new ActionableItem("Wallet Coins", "Display wallet coins", () => { WalletCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Coins", "UTXO", }) { Icon = "wallet_coins", IsDefault = true, Priority = 6 },
			new ActionableItem("Wallet Stats", "Display wallet stats", () => { WalletStatsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Stats", }) { Icon = "stats_wallet_regular", IsDefault = true, Priority = 7 },
			new ActionableItem("Wallet Info", "Display wallet info", () => { WalletInfoCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Info", }) { Icon = "info_regular", IsDefault = true, Priority = 8 },
		};
	}

	private ISearchItem CreateSendItem()
	{
		return new ActionableItem("Send", "Display wallet send dialog", () => { DefaultSendCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Send", "Action", }) { Icon = "wallet_action_send", IsDefault = true, Priority = 1 };
	}

	private ISearchItem CreateDonateItem()
	{
		return new ActionableItem("Donate", "Donate to The Wasabi Wallet Developers", () => { DonateCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Send", "Action", "Donate" }) { Icon = "gift", IsDefault = true, Priority = 4 };
	}

	private IEnumerable<ActivatableViewModel> GetTiles()
	{
		yield return new WalletBalanceTileViewModel(WalletModel.Balances);

		if (!IsWatchOnly)
		{
			yield return new PrivacyControlTileViewModel(UiContext, WalletModel);
		}

		yield return new BtcPriceTileViewModel(UiContext.AmountProvider);
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (WalletModel.Auth.HasPassword)
		{
			return await Navigate().To().PasswordAuthDialog(WalletModel).GetResultAsync();
		}

		return true;
	}

	private async Task DelaySwitchHasMusicBoxBeenDisplayedAsync(CancellationToken cancellationToken)
	{
		await Task.Delay(10000, cancellationToken);
		HasMusicBoxBeenDisplayed = true;
	}
}
