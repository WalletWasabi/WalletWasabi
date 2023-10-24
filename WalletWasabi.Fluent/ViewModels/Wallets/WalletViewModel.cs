using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletViewModel : RoutableViewModel, IWalletViewModel
{
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isSelected;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isWalletBalanceZero;

	//[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isTransactionHistoryEmpty;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isSendButtonVisible;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private bool _isLoading;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private bool _isCoinJoining;

	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private WalletState _walletState;

	public WalletViewModel(UiContext uiContext, IWalletModel walletModel, Wallet wallet)
	{
		UiContext = uiContext;
		WalletModel = walletModel;
		Wallet = wallet;

		_title = WalletName;

		// TODO:
		//this.WhenAnyValue(x => x.IsCoinJoining)
		//	.Skip(1)
		//	.Subscribe(_ => MainViewModel.Instance.InvalidateIsCoinJoinActive());

		Disposables = Disposables is null
			? new CompositeDisposable()
			: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

		Settings = new WalletSettingsViewModel(UiContext, WalletModel);
		CoinJoinSettings = new CoinJoinSettingsViewModel(UiContext, WalletModel);
		History = new HistoryViewModel(UiContext, this, WalletModel);

		walletModel.HasBalance
				   .Select(x => !x)
				   .BindTo(this, x => x.IsWalletBalanceZero)
				   .DisposeWith(Disposables);

		walletModel.Coinjoin.IsRunning
							.BindTo(this, x => x.IsCoinJoining)
							.DisposeWith(Disposables);

		//this.WhenAnyValue(x => x.History.IsTransactionHistoryEmpty)
		//	.Subscribe(x => IsTransactionHistoryEmpty = x);

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

		WalletSettingsCommand = ReactiveCommand.Create(() => Navigate().To(Settings));

		WalletCoinsCommand = ReactiveCommand.Create(() => Navigate().To().WalletCoins(WalletModel));

		CoinJoinSettingsCommand = ReactiveCommand.Create(() => Navigate(NavigationTarget.DialogScreen).To(CoinJoinSettings), Observable.Return(!WalletModel.IsWatchOnlyWallet));

		CoinJoinStateViewModel = new CoinJoinStateViewModel(uiContext, WalletModel);

		Tiles = GetTiles().ToList();

		this.WhenAnyValue(x => x.Settings.PreferPsbtWorkflow)
			.Do(x => this.RaisePropertyChanged(nameof(PreferPsbtWorkflow)))
			.Subscribe();
	}

	private string _title;

	// TODO: Remove this
	public Wallet Wallet { get; }

	public IWalletModel WalletModel { get; }

	public string WalletName => WalletModel.Name;

	public bool IsLoggedIn => WalletModel.Auth.IsLoggedIn;

	public bool PreferPsbtWorkflow => WalletModel.Settings.PreferPsbtWorkflow;

	public override string ToString() => WalletName;

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

	private CompositeDisposable Disposables { get; set; }

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
