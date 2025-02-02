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
using Unit = System.Reactive.Unit;

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
	[AutoNotify] private string _anonScoreTarget;
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private IWalletModel _selectedOutputWallet;
	[AutoNotify] private ReadOnlyObservableCollection<IWalletModel> _wallets = ReadOnlyObservableCollection<IWalletModel>.Empty;
	[AutoNotify] private bool _isOutputWalletSelectionEnabled = true;
	[AutoNotify] private bool _maximizePrivacyProfileSelected;
	[AutoNotify] private bool _speedyProfileSelected;
	[AutoNotify] private bool _economicalProfileSelected;

	private CompositeDisposable _disposable = new();

	public WalletCoinJoinSettingsViewModel(UiContext uiContext, IWalletModel walletModel)
	{
		UiContext = uiContext;
		_wallet = walletModel;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		_plebStopThreshold = _wallet.Settings.PlebStopThreshold.ToString();
		_anonScoreTarget = _wallet.Settings.AnonScoreTarget.ToString();
		_redCoinIsolation = _wallet.Settings.RedCoinIsolation;
		_timeFrames = new[]
		{
			new TimeFrameItem("Hours", TimeSpan.Zero),
			new TimeFrameItem("Days", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[0])),
			new TimeFrameItem("Weeks", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[1])),
			new TimeFrameItem("Months", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[2]))
		};
		_selectedTimeFrame = _timeFrames.FirstOrDefault(tf => tf.TimeFrame == TimeSpan.FromHours(_wallet.Settings.FeeRateMedianTimeFrameHours)) ?? _timeFrames.First();

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

		SelectMaximizePrivacySettings = ReactiveCommand.CreateFromTask(SetMaximizePrivacySettings);

		SelectSpeedySettings = ReactiveCommand.CreateFromTask(SetSpeedySettings);

		SelectEconomicalSettings = ReactiveCommand.CreateFromTask(SetEconomicalSettings);

		MaximizePrivacyProfileSelected = IsMaximizePrivacySettings;
		SpeedyProfileSelected = IsSpeedySettings;
		EconomicalProfileSelected = IsEconomicalSettings;

		this.WhenAnyValue(
				x => x.AnonScoreTarget,
				x => x.PlebStopThreshold,
				x => x.RedCoinIsolation,
				x => x.SelectedTimeFrame,
				(_, _, _, _) => Unit.Default)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				MaximizePrivacyProfileSelected = IsMaximizePrivacySettings;
				SpeedyProfileSelected = IsSpeedySettings;
				EconomicalProfileSelected = IsEconomicalSettings;
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
		if (int.TryParse(AnonScoreTarget, out int anonScoreTarget))
		{
			if (anonScoreTarget < 2 || anonScoreTarget > 300)
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

	private Task SetMaximizePrivacySettings()
	{
		AnonScoreTarget = DefaultAnonScoreTargetHelper.GetDefaultAnonScoreTarget().ToString();
		RedCoinIsolation = true;
		SelectedTimeFrame = TimeFrames.First();
		_wallet.Settings.Save();
		return Task.CompletedTask;
	}

	private bool IsMaximizePrivacySettings => int.Parse(AnonScoreTarget) > DefaultAnonScoreTargetHelper.MinExclusive &&
			int.Parse(AnonScoreTarget) < DefaultAnonScoreTargetHelper.MaxExclusive &&
			RedCoinIsolation &&
			SelectedTimeFrame == TimeFrames.First();

	private Task SetSpeedySettings()
	{
		AnonScoreTarget = "5";
		RedCoinIsolation = false;
		SelectedTimeFrame = TimeFrames.First();
		_wallet.Settings.Save();
		return Task.CompletedTask;
	}

	private bool IsSpeedySettings => AnonScoreTarget == "5" &&
		       !RedCoinIsolation &&
		       SelectedTimeFrame == TimeFrames.First();

	private Task SetEconomicalSettings()
	{
		AnonScoreTarget = "5";
		RedCoinIsolation = false;
		SelectedTimeFrame = TimeFrames[2];
		_wallet.Settings.Save();
		return Task.CompletedTask;
	}

	private bool IsEconomicalSettings => AnonScoreTarget == "5" &&
		       !RedCoinIsolation &&
		       SelectedTimeFrame == TimeFrames[2];

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
