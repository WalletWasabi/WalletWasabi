using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
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
using WalletWasabi.Fluent.ViewModels.Wallets.Buy;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Settings;
using WalletWasabi.Wallets;
using ScriptType = WalletWasabi.Fluent.Models.Wallets.ScriptType;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[AppLifetime]
public partial class WalletViewModel : RoutableViewModel, IWalletViewModel
{
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isCoinJoining;

	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isLoading;
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isSelected;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;

	private string _title = "";
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private WalletState _walletState;

	private UiConfig _uiConfig { get; }

	public WalletViewModel(UiContext uiContext, IWalletModel walletModel, Wallet wallet)
	{
		UiContext = uiContext;
		WalletModel = walletModel;
		Wallet = wallet;

		Settings = new WalletSettingsViewModel(UiContext, WalletModel);
		History = new HistoryViewModel(UiContext, WalletModel);
		BuyViewModel = new BuyViewModel(UiContext, WalletModel);

		_uiConfig = Services.UiConfig;

		var searchItems = CreateSearchItems();
		this.WhenAnyValue(x => x.IsSelected)
			.Do(shouldDisplay => UiContext.EditableSearchSource.Toggle(searchItems, shouldDisplay))
			.Subscribe();

		var sendSearchItem = CreateSendItem();
		this.WhenAnyValue(x => x.IsSendButtonVisible, x => x.IsSelected, (x, y) => x && y)
			.Do(shouldAdd => UiContext.EditableSearchSource.Toggle(sendSearchItem, shouldAdd))
			.Subscribe();

		walletModel.HasBalance
			.Select(x => !x)
			.BindTo(this, x => x.IsWalletBalanceZero);

		walletModel.Coinjoin.IsRunning
			.BindTo(this, x => x.IsCoinJoining);

		this.WhenAnyValue(x => x.IsWalletBalanceZero)
			.Subscribe(_ => IsSendButtonVisible = !IsWalletBalanceZero && (!WalletModel.IsWatchOnlyWallet || WalletModel.IsHardwareWallet));

		IsMusicBoxVisible =
			this.WhenAnyValue(x => x.IsSelected, x => x.IsWalletBalanceZero, x => x.CoinJoinStateViewModel.AreAllCoinsPrivate, x => x.IsPointerOver)
				.Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
				.Select(tuple =>
				{
					var (isSelected, isWalletBalanceZero, areAllCoinsPrivate, pointerOver) = tuple;
					return (isSelected && !isWalletBalanceZero && (!areAllCoinsPrivate || pointerOver)) && !WalletModel.IsWatchOnlyWallet;
				});

		SendCommand = ReactiveCommand.Create(() => Navigate().To().Send(walletModel, new SendFlowModel(wallet, walletModel)));
		SendManualControlCommand = ReactiveCommand.Create(() => Navigate().To().ManualControlDialog(walletModel, wallet));

		DefaultReceiveCommand = ReactiveCommand.Create(() => Navigate().To().Receive(WalletModel, ReceiveScriptType));
		SegwitReceiveCommand = ReactiveCommand.Create(() =>
		{
			_uiConfig.ReceiveScriptType = ScriptType.SegWit.Name;
			Navigate().To().Receive(WalletModel, ScriptType.SegWit);
		});
		TaprootReceiveCommand = SeveralReceivingScriptTypes ?
			ReactiveCommand.Create(() =>
		{
			_uiConfig.ReceiveScriptType = ScriptType.Taproot.Name;
			Navigate().To().Receive(WalletModel, ScriptType.Taproot);
		}) :
			null;

		BuyCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(BuyViewModel));

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

		CoinJoinStateViewModel = new CoinJoinStateViewModel(uiContext, WalletModel, Settings);

		Tiles = GetTiles().ToList();

		CanBuy =
			walletModel.HasBalance
				.CombineLatest(BuyViewModel.HasNonEmptyOrder)
				.Select(x => GetIsBuyButtonVisible(x.First, x.Second));

		HasUnreadConversations = BuyViewModel.Orders
			.ToObservableChangeSet(x => x.OrderNumber)
			.AutoRefresh(x => x.HasUnreadMessages)
			.Filter(model => model.HasUnreadMessages)
			.AsObservableCache()
			.CountChanged
			.Select(x => x > 0);

		this.WhenAnyValue(x => x.Settings.PreferPsbtWorkflow)
			.Do(x => this.RaisePropertyChanged(nameof(PreferPsbtWorkflow)))
			.Subscribe();

		this.WhenAnyValue(x => x._uiConfig.ReceiveScriptType)
			.Do(x => this.RaisePropertyChanged(nameof(ReceiveScriptType)))
			.Subscribe();

		this.WhenAnyValue(x => x.WalletModel.Name).BindTo(this, x => x.Title);
	}

	public ICommand BuyCommand { get; set; }

	// TODO: Remove this
	public Wallet Wallet { get; }

	public IWalletModel WalletModel { get; }

	public bool IsLoggedIn => WalletModel.Auth.IsLoggedIn;

	public bool PreferPsbtWorkflow => WalletModel.Settings.PreferPsbtWorkflow;

	public bool SeveralReceivingScriptTypes => WalletModel.AvailableScriptPubKeyTypes.Contains(ScriptPubKeyType.TaprootBIP86);
	public ScriptType ReceiveScriptType => SeveralReceivingScriptTypes ?
		ScriptType.FromString(_uiConfig.ReceiveScriptType) :
		ScriptType.SegWit;

	public bool IsWatchOnly => WalletModel.IsWatchOnlyWallet;

	public IObservable<bool> IsMusicBoxVisible { get; }

	public CoinJoinStateViewModel CoinJoinStateViewModel { get; private set; }

	public WalletSettingsViewModel Settings { get; private set; }

	public HistoryViewModel History { get; }

	public BuyViewModel BuyViewModel { get; }

	public IObservable<bool> CanBuy { get; }

	public IEnumerable<ActivatableViewModel> Tiles { get; }

	public ICommand SendCommand { get; private set; }

	public ICommand SendManualControlCommand { get; }

	public ICommand? BroadcastPsbtCommand { get; set; }

	public ICommand DefaultReceiveCommand { get; private set; }
	public ICommand SegwitReceiveCommand { get; private set; }
	public ICommand? TaprootReceiveCommand { get; private set; }

	public ICommand WalletInfoCommand { get; private set; }

	public ICommand WalletSettingsCommand { get; private set; }

	public ICommand WalletStatsCommand { get; private set; }

	public ICommand WalletCoinsCommand { get; private set; }

	public ICommand CoinJoinSettingsCommand { get; private set; }

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	public IObservable<bool> HasUnreadConversations { get; }

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

		WalletModel.State
			.BindTo(this, x => x.WalletState)
			.DisposeWith(disposables);
	}

	private bool GetIsBuyButtonVisible(bool hasBalance, bool hasNonEmptyOrder)
	{
#if DEBUG
		return true;
#endif

		if (hasBalance || hasNonEmptyOrder)
		{
			return true;
		}

		return false;
	}

	private ISearchItem[] CreateSearchItems()
	{
		return new ISearchItem[]
		{
			new ActionableItem("Receive", "Display wallet receive dialog", () => { DefaultReceiveCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Receive", "Action", }) { Icon = "wallet_action_receive", IsDefault = true, Priority = 2 },
			new ActionableItem("Coinjoin Settings", "Display wallet coinjoin settings", () => { CoinJoinSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Settings", }) { Icon = "wallet_action_coinjoin", IsDefault = true, Priority = 3 },
			new ActionableItem("Wallet Settings", "Display wallet settings", () => { WalletSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Settings", }) { Icon = "settings_wallet_regular", IsDefault = true, Priority = 4 },
			new ActionableItem("Exclude Coins", "Display exclude coins", () => { CoinJoinStateViewModel.NavigateToExcludedCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Exclude", "Coins", "Coinjoin", "Freeze", "UTXO", }) { Icon = "exclude_coins", IsDefault = true, Priority = 5 },
			new ActionableItem("Wallet Coins", "Display wallet coins", () => { WalletCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Coins", "UTXO", }) { Icon = "wallet_coins", IsDefault = true, Priority = 6 },
			new ActionableItem("Wallet Stats", "Display wallet stats", () => { WalletStatsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Stats", }) { Icon = "stats_wallet_regular", IsDefault = true, Priority = 7 },
			new ActionableItem("Wallet Info", "Display wallet info", () => { WalletInfoCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Info", }) { Icon = "info_regular", IsDefault = true, Priority = 8 },
		};
	}

	private ISearchItem CreateSendItem()
	{
		return new ActionableItem("Send", "Display wallet send dialog", () => { SendCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Send", "Action", }) { Icon = "wallet_action_send", IsDefault = true, Priority = 1 };
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
}
