using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletViewModel : RoutableViewModel, IWalletViewModel
{
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isSelected;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isLoading;
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isCoinJoining;
	[AutoNotify(SetterModifier = AccessModifier.Protected)] private WalletState _walletState;

	private string _title = "";

	public WalletViewModel(UiContext uiContext, IWalletModel walletModel, Wallet wallet)
	{
		UiContext = uiContext;
		WalletModel = walletModel;
		Wallet = wallet;

		Settings = new WalletSettingsViewModel(UiContext, WalletModel);
		CoinJoinSettings = new CoinJoinSettingsViewModel(UiContext, WalletModel);
		History = new HistoryViewModel(UiContext, WalletModel);

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

		SendCommand = ReactiveCommand.Create(() => Navigate().To().Send(this));

		ReceiveCommand = ReactiveCommand.Create(() => Navigate().To().Receive(WalletModel));

		WalletInfoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (await AuthorizeForPasswordAsync())
			{
				Navigate().To().WalletInfo(WalletModel);
			}
		});

		WalletStatsCommand = ReactiveCommand.Create(() => Navigate().To().WalletStats(WalletModel));

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(Settings));

		WalletCoinsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To().WalletCoins(WalletModel));

		CoinJoinSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(CoinJoinSettings), Observable.Return(!WalletModel.IsWatchOnlyWallet));

		CoinJoinStateViewModel = new CoinJoinStateViewModel(uiContext, WalletModel);

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

	public CoinJoinSettingsViewModel CoinJoinSettings { get; private set; }

	public bool IsWatchOnly => WalletModel.IsWatchOnlyWallet;

	public IObservable<bool> IsMusicBoxVisible { get; }

	public CoinJoinStateViewModel CoinJoinStateViewModel { get; private set; }

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

	public override string Title
	{
		get => _title;
		protected set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	private ISearchItem[] CreateSearchItems()
	{
		return new ISearchItem[]
		{
			new ActionableItem("Receive", "Display wallet receive dialog", () => { ReceiveCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Receive", "Action", }) { Icon = "wallet_action_receive", IsDefault = true, Priority = 2 },
			new ActionableItem("Coinjoin Settings", "Display wallet coinjoin settings", () => { CoinJoinSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Settings", }) { Icon = "wallet_action_coinjoin", IsDefault = true, Priority = 3 },
			new ActionableItem("Wallet Settings", "Display wallet settings", () => { WalletSettingsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Settings", }) { Icon = "settings_wallet_regular", IsDefault = true, Priority = 4 },
			new ActionableItem("Wallet Coins", "Display wallet coins", () => { WalletCoinsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Coins", "UTXO", }) { Icon = "wallet_coins", IsDefault = true, Priority = 5 },
			new ActionableItem("Wallet Stats", "Display wallet stats", () => { WalletStatsCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Stats", }) { Icon = "stats_wallet_regular", IsDefault = true, Priority = 6 },
			new ActionableItem("Wallet Info", "Display wallet info", () => { WalletInfoCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Info", }) { Icon = "info_regular", IsDefault = true, Priority = 7 },
		};
	}

	private ISearchItem CreateSendItem()
	{
		return new ActionableItem("Send", "Display wallet send dialog", () => { SendCommand.ExecuteIfCan(); return Task.CompletedTask; }, "Wallet", new[] { "Wallet", "Send", "Action", }) { Icon = "wallet_action_send", IsDefault = true, Priority = 1 };
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

	public void SelectTransaction(uint256 txid)
	{
		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			await Task.Delay(500);
			History.SelectTransaction(txid);
		});
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
