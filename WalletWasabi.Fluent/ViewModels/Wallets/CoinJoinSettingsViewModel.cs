using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(
	Title = "Coinjoin Settings",
	Caption = "Display wallet coinjoin settings",
	IconName = "nav_wallet_24_regular",
	Order = 1,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Settings", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinSettingsViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private bool _isCoinjoinProfileSelected;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private string? _selectedCoinjoinProfileName;

	public CoinJoinSettingsViewModel(WalletViewModel walletViewModelBase)
	{
		_wallet = walletViewModelBase.Wallet;
		_autoCoinJoin = _wallet.KeyManager.AutoCoinJoin;
		_plebStopThreshold = _wallet.KeyManager.PlebStopThreshold?.ToString() ?? KeyManager.DefaultPlebStopThreshold.ToString();
		_anonScoreTarget = _wallet.AnonScoreTarget;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		SetAutoCoinJoin = ReactiveCommand.CreateFromTask(
			async () =>
			{
				if (_wallet.KeyManager.IsCoinjoinProfileSelected)
				{
					AutoCoinJoin = !AutoCoinJoin;
				}
				else
				{
					await NavigateDialogAsync(
						new CoinJoinProfilesViewModel(_wallet.KeyManager, false),
						NavigationTarget.DialogScreen);
				}

				if (_wallet.KeyManager.IsCoinjoinProfileSelected)
				{
					_wallet.KeyManager.AutoCoinJoin = AutoCoinJoin;
					_wallet.KeyManager.ToFile();
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
					if (Money.TryParse(x, out var result) && result != _wallet.KeyManager.PlebStopThreshold)
					{
						_wallet.KeyManager.PlebStopThreshold = result;
						_wallet.KeyManager.ToFile();
					}
				});
	}

	public ICommand SetAutoCoinJoin { get; }

	public ICommand SelectCoinjoinProfileCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);
		PlebStopThreshold = _wallet.KeyManager.PlebStopThreshold.ToString();
		AnonScoreTarget = _wallet.AnonScoreTarget;

		IsCoinjoinProfileSelected = _wallet.KeyManager.IsCoinjoinProfileSelected;
		SelectedCoinjoinProfileName =
			(_wallet.KeyManager.IsCoinjoinProfileSelected,
			CoinJoinProfilesViewModel.IdentifySelectedProfile(_wallet.KeyManager)) switch
			{
				(true, CoinJoinProfileViewModelBase x) => x.Title,
				(false, _) => "None",
				_ => "Unknown"
			};
	}

	private async Task SelectCoinjoinProfileAsync()
	{
		await NavigateDialogAsync(
			new CoinJoinProfilesViewModel(_wallet.KeyManager, false),
			NavigationTarget.DialogScreen);
		AutoCoinJoin = _wallet.KeyManager.AutoCoinJoin;
	}
}
