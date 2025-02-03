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
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

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

	[AutoNotify] private WalletWasabi.Helpers.CoinJoinProfiles.TimeFrameItem _selectedTimeFrame;
	[AutoNotify] private string _anonScoreTarget;
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private bool _maximizePrivacyProfileSelected;
	[AutoNotify] private bool _speedyProfileSelected;
	[AutoNotify] private bool _economicalProfileSelected;

	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private bool _isOutputWalletSelectionEnabled = true;
	[AutoNotify] private IWalletModel _selectedOutputWallet;
	[AutoNotify] private ReadOnlyObservableCollection<IWalletModel> _wallets = ReadOnlyObservableCollection<IWalletModel>.Empty;

	private CompositeDisposable _disposable = new();

	public WalletCoinJoinSettingsViewModel(UiContext uiContext, IWalletModel walletModel)
	{
		UiContext = uiContext;
		_wallet = walletModel;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		_plebStopThreshold = _wallet.Settings.PlebStopThreshold.ToString();
		_anonScoreTarget = _wallet.Settings.AnonScoreTarget.ToString();
		_redCoinIsolation = _wallet.Settings.RedCoinIsolation;

		_selectedTimeFrame = CoinJoinProfiles.TimeFrames.FirstOrDefault(tf => tf.TimeFrame == TimeSpan.FromHours(_wallet.Settings.FeeRateMedianTimeFrameHours)) ?? CoinJoinProfiles.TimeFrames.First();

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

		SetRedCoinIsolationCommand = ReactiveCommand.CreateFromTask(() =>
		{
			_wallet.Settings.RedCoinIsolation = RedCoinIsolation;
			_wallet.Settings.Save();
			return Task.CompletedTask;
		});

		SelectMaximizePrivacySettings = ReactiveCommand.CreateFromTask(() => SetProfile("MaximizePrivacy"));

		SelectSpeedySettings = ReactiveCommand.CreateFromTask(() => SetProfile("Speedy"));

		SelectEconomicalSettings = ReactiveCommand.CreateFromTask(() => SetProfile("Economical"));

		this.WhenAnyValue(
				x => x.AnonScoreTarget,
				x => x.RedCoinIsolation,
				x => x.SelectedTimeFrame)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(_ =>
			{
				var selectedProfile = CoinJoinProfiles.Profiles
					.FirstOrDefault(p =>
						p.Equals(
							int.Parse(AnonScoreTarget),
							RedCoinIsolation, SelectedTimeFrame.TimeFrame));

				MaximizePrivacyProfileSelected = selectedProfile?.Name == "MaximizePrivacy";
				EconomicalProfileSelected = selectedProfile?.Name == "Economical";
				SpeedyProfileSelected = selectedProfile?.Name == "Speedy";
			});

		this.ValidateProperty(x => x.AnonScoreTarget, ValidateAnonScoreTarget);

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

		this.WhenAnyValue(x => x.SelectedTimeFrame)
			.Skip(1)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(
				x =>
				{
					_wallet.Settings.FeeRateMedianTimeFrameHours = (int)x.TimeFrame.TotalHours;
					_wallet.Settings.Save();
				});

		ManuallyUpdateOutputWalletList();
	}

	public CoinJoinProfiles.TimeFrameItem[] TimeFrames => CoinJoinProfiles.TimeFrames;
	public ICommand SetAutoCoinJoin { get; }
	public ICommand SetRedCoinIsolationCommand { get; }
	public ICommand SelectMaximizePrivacySettings {  get; }
	public ICommand SelectSpeedySettings { get; }
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
			if (anonScoreTarget is < CoinJoinProfiles.AbsoluteMinAnonScoreTarget or > CoinJoinProfiles.AbsoluteMaxAnonScoreTarget)
			{
				errors.Add(ErrorSeverity.Error, "Target must be between 2 and 300");
			}
			else
			{
				_wallet.Settings.AnonScoreTarget = anonScoreTarget;
				_wallet.Settings.Save();
			}
		}
		else
		{
			errors.Add(ErrorSeverity.Error, "Target must be a number between 2 and 300");
		}
	}

	private Task SetProfile(string profileName)
	{
		var profile = CoinJoinProfiles.Profiles.FirstOrDefault(p => p.Name == profileName);
		if (profile is null)
		{
			return Task.CompletedTask;
		}

		AnonScoreTarget = profile.AnonScoreTarget.ToString();
		_wallet.Settings.AnonScoreTarget = profile.AnonScoreTarget;

		RedCoinIsolation = profile.RedCoinIsolation;
		_wallet.Settings.RedCoinIsolation = profile.RedCoinIsolation;

		SelectedTimeFrame = CoinJoinProfiles.TimeFrames.FirstOrDefault(tf => tf.TimeFrame == profile.TimeFrame.TimeFrame) ?? CoinJoinProfiles.TimeFrames.First();
		_wallet.Settings.FeeRateMedianTimeFrameHours = (int)profile.TimeFrame.TimeFrame.TotalHours;

		_wallet.Settings.Save();
		return Task.CompletedTask;
	}
}
