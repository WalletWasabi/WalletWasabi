using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Discoverability;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent.Models.UI;

[AppLifetime]
public partial class ApplicationSettings : ReactiveObject
{
	private const int ThrottleTime = 500;

	private readonly IServices _services;
	private readonly Subject<bool> _isRestartNeeded = new();
	private readonly string _persistentConfigFilePath;
	private readonly ApplicationSettingsData _startupSettings;
	private readonly UiConfig _uiConfig;

	// Advanced
	[AutoNotify] private bool _enableGpu;

	// Bitcoin
	[AutoNotify] private Network _network;

	[AutoNotify] private string _bitcoinRpcUri;
	[AutoNotify] private string _bitcoinRpcCredentialString;
	[AutoNotify] private string _dustThreshold;
	[AutoNotify] private string _exchangeRateProvider;
	[AutoNotify] private string _feeRateEstimationProvider;
	[AutoNotify] private string _externalTransactionBroadcaster;

	// Coordinator
	[AutoNotify] private string _coordinatorUri;
	[AutoNotify] private string _maxCoinJoinMiningFeeRate;
	[AutoNotify] private string _absoluteMinInputCount;

	// General
	[AutoNotify] private bool _darkModeEnabled;

	[AutoNotify] private bool _autoCopy;
	[AutoNotify] private bool _autoPaste;
	[AutoNotify] private bool _customChangeAddress;
	[AutoNotify] private bool _runOnSystemStartup;
	[AutoNotify] private bool _hideOnClose;
	[AutoNotify] private TorMode _useTor;
	[AutoNotify] private bool _terminateTorOnExit;
	[AutoNotify] private bool _downloadNewVersion;

	// Privacy Mode
	[AutoNotify] private bool _privacyMode;

	[AutoNotify] private bool _oobe;
	[AutoNotify] private Version _lastVersionHighlightsDisplayed;
	[AutoNotify] private WindowState _windowState;

	// Non-persistent
	[AutoNotify] private bool _doUpdateOnClose;

	// Experimental
	[AutoNotify] private string[] _experimentalFeatures;

	public ApplicationSettings(IServices services, UiConfig uiConfig)
	{
		_services = services;
		_persistentConfigFilePath = services.GetPersistentConfigFilePath();
		_startupSettings = services.GetApplicationSettings();
		_uiConfig = uiConfig;

		// Connections
		_useTor = _startupSettings.UseTor;
		_exchangeRateProvider = _startupSettings.ExchangeRateProvider;
		_feeRateEstimationProvider = _startupSettings.FeeRateEstimationProvider;
		_externalTransactionBroadcaster = _startupSettings.ExternalTransactionBroadcaster;

		// Bitcoin
		_network = _startupSettings.Network;
		_bitcoinRpcUri = _startupSettings.BitcoinRpcUri;
		_bitcoinRpcCredentialString = _startupSettings.BitcoinRpcCredentialString;
		_dustThreshold = _startupSettings.DustThreshold.ToString(CultureInfo.InvariantCulture);

		// Coordinator
		_coordinatorUri = _startupSettings.CoordinatorUri;

		_maxCoinJoinMiningFeeRate = _startupSettings.MaxCoinJoinMiningFeeRate.ToString(CultureInfo.InvariantCulture);
		_absoluteMinInputCount = _startupSettings.AbsoluteMinInputCount.ToString(CultureInfo.InvariantCulture);

		// General
		_darkModeEnabled = uiConfig.DarkModeEnabled;
		_autoCopy = uiConfig.Autocopy;
		_autoPaste = uiConfig.AutoPaste;
		_customChangeAddress = uiConfig.IsCustomChangeAddress;
		_runOnSystemStartup = uiConfig.RunOnSystemStartup;
		_hideOnClose = uiConfig.HideOnClose;
		_terminateTorOnExit = _startupSettings.TerminateTorOnExit;
		_downloadNewVersion = _startupSettings.DownloadNewVersion;
		_enableGpu = _startupSettings.EnableGpu;

		// Experimental
		_experimentalFeatures = _startupSettings.ExperimentalFeatures.ToArray();

		// Privacy Mode
		_privacyMode = uiConfig.PrivacyMode;

		_oobe = uiConfig.Oobe;
		_lastVersionHighlightsDisplayed = uiConfig.LastVersionHighlightsDisplayed;

		_windowState = Enum.Parse<WindowState>(uiConfig.WindowState);
		SetupObservables();
	}

	private void ApplyConfigs(ApplicationSettingsData settings, UiConfig uiConfig)
	{
		// Connections
		UseTor = settings.UseTor;
		ExchangeRateProvider = settings.ExchangeRateProvider;
		FeeRateEstimationProvider = settings.FeeRateEstimationProvider;
		ExternalTransactionBroadcaster = settings.ExternalTransactionBroadcaster;

		// Bitcoin
		Network = settings.Network;
		BitcoinRpcUri = settings.BitcoinRpcUri;
		BitcoinRpcCredentialString = settings.BitcoinRpcCredentialString;
		DustThreshold = settings.DustThreshold.ToString(CultureInfo.InvariantCulture);

		// Coordinator
		CoordinatorUri = settings.CoordinatorUri;

		MaxCoinJoinMiningFeeRate = settings.MaxCoinJoinMiningFeeRate.ToString(CultureInfo.InvariantCulture);
		AbsoluteMinInputCount = settings.AbsoluteMinInputCount.ToString(CultureInfo.InvariantCulture);

		// General
		DarkModeEnabled = uiConfig.DarkModeEnabled;
		AutoCopy = uiConfig.Autocopy;
		AutoPaste = uiConfig.AutoPaste;
		CustomChangeAddress = uiConfig.IsCustomChangeAddress;
		RunOnSystemStartup = uiConfig.RunOnSystemStartup;
		HideOnClose = uiConfig.HideOnClose;
		TerminateTorOnExit = settings.TerminateTorOnExit;
		DownloadNewVersion = settings.DownloadNewVersion;
		EnableGpu = settings.EnableGpu;

		// Experimental
		ExperimentalFeatures = settings.ExperimentalFeatures.ToArray();

		// Privacy Mode
		PrivacyMode = uiConfig.PrivacyMode;

		Oobe = uiConfig.Oobe;
		LastVersionHighlightsDisplayed = uiConfig.LastVersionHighlightsDisplayed;

		WindowState = Enum.Parse<WindowState>(uiConfig.WindowState);
	}

	private void SetupObservables()
	{
		// Save on change
		var configSaveTrigger1 =
			this.WhenAnyValue(
					x => x.EnableGpu,
					x => x.Network,
					x => x.BitcoinRpcCredentialString,
					x => x.BitcoinRpcUri,
					x => x.DustThreshold,
					x => x.UseTor,
					x => x.TerminateTorOnExit,
					x => x.DownloadNewVersion,
					(_, _, _, _, _, _, _, _) => Unit.Default)
				.Skip(1);
		var configSaveTrigger2 =
			this.WhenAnyValue(
					x => x.MaxCoinJoinMiningFeeRate,
					x => x.AbsoluteMinInputCount,
					x => x.CoordinatorUri,
					x => x.ExchangeRateProvider,
					x => x.FeeRateEstimationProvider,
					x => x.ExternalTransactionBroadcaster,
					(_, _, _, _, _, _) => Unit.Default)
				.Skip(1);

		Observable
			.Merge(configSaveTrigger1, configSaveTrigger2)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => Save())
			.Subscribe();


		// Save UiConfig on change
		this.WhenAnyValue(
				x => x.DarkModeEnabled,
				x => x.AutoCopy,
				x => x.AutoPaste,
				x => x.CustomChangeAddress,
				x => x.RunOnSystemStartup,
				x => x.HideOnClose,
				x => x.Oobe,
				x => x.LastVersionHighlightsDisplayed,
				x => x.WindowState)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => ApplyUiConfigChanges())
			.Subscribe();

		// Save UiConfig on change without throttling
		this.WhenAnyValue(x => x.PrivacyMode)
			.Skip(1)
			.Do(_ => ApplyUiConfigPrivacyModeChange())
			.Subscribe();

		// Apply RunOnSystemStartup
		this.WhenAnyValue(x => x.RunOnSystemStartup)
			.DoAsync(async _ => await StartupHelper.ModifyStartupSettingAsync(RunOnSystemStartup))
			.Subscribe();

		// Apply DoUpdateOnClose
		this.WhenAnyValue(x => x.DoUpdateOnClose)
			.Do(x => _services.PublishEvent(new InstallOnClosedPreferenceChanged(x)))
			.Subscribe();
	}

	public void ResetToDefault()
	{
		var defaultSettings = _services.GetDefaultApplicationSettings(_startupSettings.Network);
		var newSettings = defaultSettings with { CoordinatorUri = CoordinatorUri };

		var newUiConfig = new UiConfig(_uiConfig.FilePath)
		{
			Oobe = Oobe,
			LastVersionHighlightsDisplayed = LastVersionHighlightsDisplayed
		};

		ApplyConfigs(newSettings, newUiConfig);
	}

	public bool IsOverridden => _startupSettings.IsOverridden;

	public IObservable<bool> IsRestartNeeded => _isRestartNeeded;

	private void Save()
	{
		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					var settings = BuildCurrentSettings();
					var restartNeeded = _services.SaveApplicationSettings(settings);
					_isRestartNeeded.OnNext(restartNeeded);
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	private ApplicationSettingsData BuildCurrentSettings() => new()
	{
		EnableGpu = EnableGpu,
		Network = Network,
		BitcoinRpcUri = BitcoinRpcUri,
		BitcoinRpcCredentialString = BitcoinRpcCredentialString,
		DustThreshold = decimal.TryParse(DustThreshold, out var threshold) ? threshold : Constants.DefaultDustThreshold,
		CoordinatorUri = CoordinatorUri,
		MaxCoinJoinMiningFeeRate = decimal.TryParse(MaxCoinJoinMiningFeeRate, out var maxCoinjoinMiningFeeRate) ? maxCoinjoinMiningFeeRate : Constants.DefaultMaxCoinJoinMiningFeeRate,
		AbsoluteMinInputCount = int.TryParse(AbsoluteMinInputCount, out var absoluteMinInputCount) ? absoluteMinInputCount : Constants.DefaultAbsoluteMinInputCount,
		ExchangeRateProvider = ExchangeRateProvider,
		FeeRateEstimationProvider = FeeRateEstimationProvider,
		ExternalTransactionBroadcaster = ExternalTransactionBroadcaster,
		UseTor = UseTor,
		TerminateTorOnExit = TerminateTorOnExit,
		DownloadNewVersion = DownloadNewVersion,
		ExperimentalFeatures = ExperimentalFeatures,
		IsOverridden = _startupSettings.IsOverridden
	};

	public bool TryProcessCoordinatorConnectionString(CoordinatorConnectionString coordinatorConnectionString)
	{
		// Sanity checks
		if (coordinatorConnectionString.AbsoluteMinInputCount < Constants.AbsoluteMinInputCount)
		{
			Logger.LogWarning($"New intended absolute min input count was {coordinatorConnectionString.AbsoluteMinInputCount}, but absolute min is {Constants.AbsoluteMinInputCount}");
			return false;
		}

		CoordinatorUri = coordinatorConnectionString.CoordinatorUri.ToString();

		AbsoluteMinInputCount = coordinatorConnectionString.AbsoluteMinInputCount.ToString();

		// TODO: Save Name and ReadMoreUri to display it after.

		return true;
	}

	private void ApplyUiConfigChanges()
	{
		_uiConfig.DarkModeEnabled = DarkModeEnabled;
		_uiConfig.Autocopy = AutoCopy;
		_uiConfig.AutoPaste = AutoPaste;
		_uiConfig.IsCustomChangeAddress = CustomChangeAddress;
		_uiConfig.RunOnSystemStartup = RunOnSystemStartup;
		_uiConfig.HideOnClose = HideOnClose;
		_uiConfig.Oobe = Oobe;
		_uiConfig.LastVersionHighlightsDisplayed = LastVersionHighlightsDisplayed;
		_uiConfig.WindowState = WindowState.ToString();
	}

	private void ApplyUiConfigPrivacyModeChange()
	{
		_uiConfig.PrivacyMode = PrivacyMode;
	}
}
