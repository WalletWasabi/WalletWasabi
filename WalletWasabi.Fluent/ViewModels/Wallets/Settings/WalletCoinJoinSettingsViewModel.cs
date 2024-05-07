using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(
	Title = "Coinjoin Settings",
	Caption = "Display wallet coinjoin settings",
	IconName = "nav_wallet_24_regular",
	Order = 1,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Settings", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletCoinJoinSettingsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private bool _isCoinjoinProfileSelected;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private string? _selectedCoinjoinProfileName;
	[AutoNotify] private IWalletModel _selectedOutputWalletName;
	[AutoNotify] private ReadOnlyObservableCollection<IWalletModel> _wallets = ReadOnlyObservableCollection<IWalletModel>.Empty;
	[AutoNotify] private bool _notMatchOutputWallet;
	[AutoNotify] private bool _isEnableOutputWalletChoose = true;
	[AutoNotify] private string _userNotifyText;
	[AutoNotify] private bool _isUserNotifyVisible;

	private CompositeDisposable _disposable = new();

	public WalletCoinJoinSettingsViewModel(UiContext uiContext, IWalletModel walletModel)
	{
		UiContext = uiContext;
		_wallet = walletModel;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		_plebStopThreshold = _wallet.Settings.PlebStopThreshold.ToString();
		_anonScoreTarget = _wallet.Settings.AnonScoreTarget;
		_selectedOutputWalletName = UiContext.WalletRepository.Wallets.Items.First(x => x.Wallet.WalletId == _wallet.Settings.OutputWalletId);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		SetAutoCoinJoin = ReactiveCommand.CreateFromTask(
			async () =>
			{
				if (_wallet.Settings.IsCoinjoinProfileSelected)
				{
					AutoCoinJoin = !AutoCoinJoin;
				}
				else
				{
					await Navigate().To().CoinJoinProfiles(_wallet.Settings).GetResultAsync();
				}

				if (_wallet.Settings.IsCoinjoinProfileSelected)
				{
					_wallet.Settings.AutoCoinjoin = AutoCoinJoin;
					_wallet.Settings.Save();
				}
				else
				{
					AutoCoinJoin = false;
				}
			});

		SelectCoinjoinProfileCommand = ReactiveCommand.CreateFromTask(SelectCoinjoinProfileAsync);

		this.WhenAnyValue(x => x.PlebStopThreshold)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(1000))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
				{
					if (Money.TryParse(x, out var result) && result != _wallet.Settings.PlebStopThreshold)
					{
						_wallet.Settings.PlebStopThreshold = result;
						_wallet.Settings.Save();
					}
				});

		this.WhenAnyValue(x => x.SelectedOutputWalletName)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
				{
					_wallet.Settings.OutputWalletId = x.Wallet.WalletId;
					_wallet.Settings.Save();
				});

		this.WhenAnyValue(x => x.SelectedOutputWalletName).Select(x => x.Id != _wallet.Id)
			.BindTo(this, x => x.NotMatchOutputWallet);

		walletModel.Coinjoin.IsRunning.Select(isRunning => !isRunning)
			.BindTo(this, x => x.IsEnableOutputWalletChoose);

		this.WhenAnyValue(
				x => x.NotMatchOutputWallet,
				x => x.IsEnableOutputWalletChoose)
			.Select(tuple =>
			{
				var (notMatchOutputWallet, isEnableOutputWalletChoose) = tuple;
				var notifyText = string.Empty;

				if (notMatchOutputWallet)
				{
					notifyText = "After a CoinJoin transaction is completed, the coins should be transferred to a selected output wallet. However, this setting resets after a restart.";
				}

				if (!isEnableOutputWalletChoose)
				{
					notifyText += (notifyText == string.Empty ? "" : "\r\n\r\n") + "Until coinjoin is running you can't set the output wallet.";
				}

				return notifyText;
			})
			.BindTo(this, x => x.UserNotifyText);

		this.WhenAnyValue(
				x => x.NotMatchOutputWallet,
				x => x.IsEnableOutputWalletChoose)
			.Select(tuple =>
			{
				var (notMatchOutputWallet, isEnableOutputWalletChoose) = tuple;

				// Define visibility logic based on these conditions
				return notMatchOutputWallet || !isEnableOutputWalletChoose;
			})
			.BindTo(this, x => x.IsUserNotifyVisible);

		Update();
		ManuallyUpdateOutputWalletList();
	}

	public ICommand SetAutoCoinJoin { get; }

	public ICommand SelectCoinjoinProfileCommand { get; }

	public void ManuallyUpdateOutputWalletList()
	{
		_disposable.Dispose();
		_disposable = new CompositeDisposable();

		UiContext.WalletRepository.Wallets
			.Connect()
			.Filter(x => (x.Id == _wallet.Id || x.Settings.OutputWalletId != _wallet.Id) && x.IsLoggedIn)

			.SortBy(i => i.Name)
			.Bind(out var wallets)
			.Subscribe()
			.DisposeWith(_disposable);

		_wallets = wallets;
	}

	private void Update()
	{
		PlebStopThreshold = _wallet.Settings.PlebStopThreshold.ToString();
		AnonScoreTarget = _wallet.Settings.AnonScoreTarget;

		IsCoinjoinProfileSelected = _wallet.Settings.IsCoinjoinProfileSelected;
		SelectedCoinjoinProfileName =
			(_wallet.Settings.IsCoinjoinProfileSelected,
					CoinJoinProfilesViewModel.IdentifySelectedProfile(_wallet.Settings)) switch
			{
				(true, CoinJoinProfileViewModelBase x) => x.Title,
				(false, _) => "None",
				_ => "Unknown"
			};
	}

	private async Task SelectCoinjoinProfileAsync()
	{
		await Navigate().To().CoinJoinProfiles(_wallet.Settings).GetResultAsync();
		AutoCoinJoin = _wallet.Settings.AutoCoinjoin;
		Update();
	}
}
