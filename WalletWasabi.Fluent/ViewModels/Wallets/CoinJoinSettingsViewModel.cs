using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;
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
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class CoinJoinSettingsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private bool _isCoinjoinProfileSelected;
	[AutoNotify] private string? _selectedCoinjoinProfileName;

	public CoinJoinSettingsViewModel(UiContext uiContext, IWalletModel walletModel)
	{
		_wallet = walletModel;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		var plebStopThreshold = _wallet.Settings.PlebStopThreshold.ToDecimal(MoneyUnit.BTC);
		_anonScoreTarget = _wallet.Settings.AnonScoreTarget;

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

		PlebStopThreshold = new CurrencyInputViewModel(uiContext, CurrencyFormat.Btc);
		PlebStopThreshold.SetValue(plebStopThreshold);

		this.WhenAnyValue(x => x.PlebStopThreshold.Value)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(1000))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
				{
					if (x.ToDecimal() is { } value && value != _wallet.Settings.PlebStopThreshold.ToDecimal(MoneyUnit.BTC))
					{
						_wallet.Settings.PlebStopThreshold = new Money(value, MoneyUnit.BTC);
						_wallet.Settings.Save();
					}
				});
	}

	public CurrencyInputViewModel PlebStopThreshold { get; }

	public ICommand SetAutoCoinJoin { get; }

	public ICommand SelectCoinjoinProfileCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);
		PlebStopThreshold.SetValue(_wallet.Settings.PlebStopThreshold.ToDecimal(MoneyUnit.BTC));
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
	}
}
