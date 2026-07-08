using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using WalletWasabi.CoinJoinProfiles;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
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

	[AutoNotify] private string _anonScoreTarget;
	[AutoNotify] private bool _nonPrivateCoinIsolation;
	[AutoNotify] private bool _maximizePrivacyProfileSelected;
	[AutoNotify] private bool _defaultProfileSelected;
	[AutoNotify] private bool _economicalProfileSelected;

	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private string _trezorMaxRounds;
	[AutoNotify] private string _trezorMaxMiningFeeRate;
	[AutoNotify] private bool _isOutputWalletSelectionEnabled = true;
	[AutoNotify] private IWalletModel _selectedOutputWallet;
	[AutoNotify] private ReadOnlyObservableCollection<IWalletModel> _wallets = ReadOnlyObservableCollection<IWalletModel>.Empty;

	private CompositeDisposable _disposable = new();

	public WalletCoinJoinSettingsViewModel(UiContext uiContext, IWalletModel walletModel) : base(uiContext)
	{
		_wallet = walletModel;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		_plebStopThreshold = _wallet.Settings.PlebStopThreshold.ToString();
		_anonScoreTarget = _wallet.Settings.AnonScoreTarget.ToString();
		_nonPrivateCoinIsolation = _wallet.Settings.NonPrivateCoinIsolation;
		IsTrezorCoinJoinWallet = _wallet.Settings.IsTrezorCoinJoinWallet;
		_trezorMaxRounds = _wallet.Settings.TrezorCoinjoinMaxRounds.ToString();
		_trezorMaxMiningFeeRate = _wallet.Settings.TrezorCoinjoinMaxMiningFeeRate.ToString(System.Globalization.CultureInfo.InvariantCulture);

		_selectedOutputWallet = UiContext.WalletRepository.Wallets.Items.First(x => x.Id == _wallet.Settings.OutputWalletId);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		SetAutoCoinJoin = ReactiveCommand.CreateFromTask(
			() =>
			{
				_wallet.Settings.AutoCoinjoin = AutoCoinJoin;
				_wallet.Settings.Save();
				return Task.CompletedTask;
			});

		SetNonPrivateCoinIsolationCommand = ReactiveCommand.CreateFromTask(() =>
		{
			_wallet.Settings.NonPrivateCoinIsolation = NonPrivateCoinIsolation;
			_wallet.Settings.Save();
			return Task.CompletedTask;
		});

		SelectMaximizePrivacySettings = ReactiveCommand.CreateFromTask(() => SetProfile("MaximizePrivacy"));

		SelectDefaultSettings = ReactiveCommand.CreateFromTask(() => SetProfile("Default"));

		SelectEconomicalSettings = ReactiveCommand.CreateFromTask(() => SetProfile("Economical"));

		this.WhenAnyValue(
				x => x.AnonScoreTarget,
				x => x.NonPrivateCoinIsolation)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(_ =>
			{
				var selectedProfile = PrivacyProfiles.Profiles
					.FirstOrDefault(p =>
						p.Equals(
							int.TryParse(AnonScoreTarget, out var anonScoreTarget) ? anonScoreTarget : 0,
							NonPrivateCoinIsolation));

				MaximizePrivacyProfileSelected = selectedProfile?.Name == "MaximizePrivacy";
				EconomicalProfileSelected = selectedProfile?.Name == "Economical";
				DefaultProfileSelected = selectedProfile?.Name == "Default";
			});

		this.ValidateProperty(x => x.AnonScoreTarget, ValidateAnonScoreTarget);
		this.ValidateProperty(x => x.TrezorMaxRounds, ValidateTrezorMaxRounds);
		this.ValidateProperty(x => x.TrezorMaxMiningFeeRate, ValidateTrezorMaxMiningFeeRate);

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

		this.WhenAnyValue(x => x.SelectedOutputWallet)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x => _wallet.Settings.OutputWalletId = x.Id);

		walletModel.IsCoinjoinStarted
			.Select(isRunning => !isRunning)
			.BindTo(this, x => x.IsOutputWalletSelectionEnabled);

		ManuallyUpdateOutputWalletList();
	}

	public bool IsTrezorCoinJoinWallet { get; }

	public ICommand SetAutoCoinJoin { get; }
	public ICommand SetNonPrivateCoinIsolationCommand { get; }
	public ICommand SelectMaximizePrivacySettings { get; }
	public ICommand SelectDefaultSettings { get; }
	public ICommand SelectEconomicalSettings { get; }

	public void ManuallyUpdateOutputWalletList()
	{
		_disposable.Dispose();
		_disposable = new CompositeDisposable();

		UiContext.WalletRepository.Wallets
			.Connect()
			.AutoRefresh(x => x.IsLoaded)
			.Filter(x => (x.Id == _wallet.Id || x.Settings.OutputWalletId != _wallet.Id) && x.IsLoaded)
			.SortBy(i => i.Name)
			.Bind(out var wallets)
			.Subscribe()
			.DisposeWith(_disposable);

		_wallets = wallets;
	}

	private void ValidateAnonScoreTarget(IValidationErrors errors)
	{
		if (int.TryParse(AnonScoreTarget, out var anonScoreTarget))
		{
			if (anonScoreTarget is < PrivacyProfiles.AbsoluteMinAnonScoreTarget or > PrivacyProfiles.AbsoluteMaxAnonScoreTarget)
			{
				errors.Add(ErrorSeverity.Error, $"Must be between {PrivacyProfiles.AbsoluteMinAnonScoreTarget} and {PrivacyProfiles.AbsoluteMaxAnonScoreTarget}");
			}
			else
			{
				_wallet.Settings.AnonScoreTarget = anonScoreTarget;
				_wallet.Settings.Save();
			}
		}
		else
		{
			errors.Add(ErrorSeverity.Error, $"Must be a number between {PrivacyProfiles.AbsoluteMinAnonScoreTarget} and {PrivacyProfiles.AbsoluteMaxAnonScoreTarget}");
		}
	}

	private void ValidateTrezorMaxRounds(IValidationErrors errors)
	{
		// Firmware caps max_rounds at 500 under strict safety checks; keep a sane user-facing range.
		if (int.TryParse(TrezorMaxRounds, out var rounds) && rounds is >= 1 and <= 500)
		{
			_wallet.Settings.TrezorCoinjoinMaxRounds = rounds;
			_wallet.Settings.Save();
		}
		else
		{
			errors.Add(ErrorSeverity.Error, "Must be a whole number between 1 and 500.");
		}
	}

	private void ValidateTrezorMaxMiningFeeRate(IValidationErrors errors)
	{
		if (decimal.TryParse(TrezorMaxMiningFeeRate, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var feeRate) && feeRate is > 0 and <= 10000)
		{
			_wallet.Settings.TrezorCoinjoinMaxMiningFeeRate = feeRate;
			_wallet.Settings.Save();
		}
		else
		{
			errors.Add(ErrorSeverity.Error, "Must be a positive fee rate in sat/vByte.");
		}
	}

	private Task SetProfile(string profileName)
	{
		var profile = PrivacyProfiles.Profiles.FirstOrDefault(p => p.Name == profileName);
		if (profile is null)
		{
			return Task.CompletedTask;
		}

		AnonScoreTarget = profile.AnonScoreTarget.ToString();
		_wallet.Settings.AnonScoreTarget = profile.AnonScoreTarget;

		NonPrivateCoinIsolation = profile.NonPrivateCoinIsolation;
		_wallet.Settings.NonPrivateCoinIsolation = profile.NonPrivateCoinIsolation;

		_wallet.Settings.Save();
		return Task.CompletedTask;
	}
}
