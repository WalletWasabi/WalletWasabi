using System.Globalization;
using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Bases;
using WalletWasabi.Daemon;
using WalletWasabi.Discoverability;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Userfacing;
using Unit = System.Reactive.Unit;

namespace WalletWasabi.Fluent.Models.UI;

[AppLifetime]
[AutoInterface]
public partial class ApplicationSettings : ReactiveObject
{
	private const int ThrottleTime = 500;

	private readonly Subject<bool> _isRestartNeeded = new();
	private readonly string _persistentConfigFilePath;
	private readonly PersistentConfig _startupConfig;
	private readonly Config _config;
	private readonly UiConfig _uiConfig;

	// Advanced
	[AutoNotify] private bool _enableGpu;
	[AutoNotify] private string _indexerUri;

	// Bitcoin
	[AutoNotify] private Network _network;

	[AutoNotify] private bool _useBitcoinRpc;
	[AutoNotify] private string _bitcoinRpcEndPoint;
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

	public ApplicationSettings(PersistentConfig persistentConfig, Config config, UiConfig uiConfig)
	{
		_persistentConfigFilePath = Services.PersistentConfigFilePath;
		_startupConfig = persistentConfig;

		_config = config;
		_uiConfig = uiConfig;

		ApplyConfigs(persistentConfig, uiConfig);
		SetupObservables();
	}

	private void ApplyConfigs(PersistentConfig persistentConfig, UiConfig uiConfig)
	{
		// Connections
		IndexerUri = persistentConfig.IndexerUri;
		UseTor = Config.ObjectToTorMode(persistentConfig.UseTor);
		ExchangeRateProvider = persistentConfig.ExchangeRateProvider;
		FeeRateEstimationProvider = persistentConfig.FeeRateEstimationProvider;
		ExternalTransactionBroadcaster = persistentConfig.ExternalTransactionBroadcaster;

		// Bitcoin
		Network = persistentConfig.Network;
		UseBitcoinRpc = persistentConfig.UseBitcoinRpc;
		BitcoinRpcEndPoint = persistentConfig.BitcoinRpcEndPoint.ToString(defaultPort: -1);
		BitcoinRpcCredentialString = persistentConfig.BitcoinRpcCredentialString;
		DustThreshold = persistentConfig.DustThreshold.ToString();

		// Coordinator
		CoordinatorUri = persistentConfig.CoordinatorUri;

		MaxCoinJoinMiningFeeRate = persistentConfig.MaxCoinJoinMiningFeeRate.ToString(CultureInfo.InvariantCulture);
		AbsoluteMinInputCount = persistentConfig.AbsoluteMinInputCount.ToString(CultureInfo.InvariantCulture);

		// General
		DarkModeEnabled = uiConfig.DarkModeEnabled;
		AutoCopy = uiConfig.Autocopy;
		AutoPaste = uiConfig.AutoPaste;
		CustomChangeAddress = uiConfig.IsCustomChangeAddress;
		RunOnSystemStartup = uiConfig.RunOnSystemStartup;
		HideOnClose = uiConfig.HideOnClose;
		TerminateTorOnExit = persistentConfig.TerminateTorOnExit;
		DownloadNewVersion = persistentConfig.DownloadNewVersion;
		EnableGpu = persistentConfig.EnableGpu;

		// Privacy Mode
		PrivacyMode = uiConfig.PrivacyMode;

		Oobe = uiConfig.Oobe;
		LastVersionHighlightsDisplayed = uiConfig.LastVersionHighlightsDisplayed;

		WindowState = (WindowState)Enum.Parse(typeof(WindowState), uiConfig.WindowState);
	}

	private void SetupObservables()
	{
		// Save on change
		var configSaveTrigger1 =
			this.WhenAnyValue(
					x => x.EnableGpu,
					x => x.Network,
					x => x.UseBitcoinRpc,
					x => x.BitcoinRpcCredentialString,
					x => x.BitcoinRpcEndPoint,
					x => x.DustThreshold,
					x => x.UseTor,
					x => x.TerminateTorOnExit,
					x => x.DownloadNewVersion,
					(_, _, _, _, _, _, _, _, _) => Unit.Default)
				.Skip(1);
		var configSaveTrigger2 =
			this.WhenAnyValue(
					x => x.MaxCoinJoinMiningFeeRate,
					x => x.AbsoluteMinInputCount,
					x => x.CoordinatorUri,
					x => x.IndexerUri,
					x => x.ExchangeRateProvider,
					x => x.FeeRateEstimationProvider,
					x => x.ExternalTransactionBroadcaster,
					(_, _, _, _, _, _, _) => Unit.Default)
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
			.Do(x => Services.EventBus.Publish(new InstallOnClosedPreferenceChanged(x)))
			.Subscribe();
	}

	public void ResetToDefault()
	{
		var defaultConfig = _startupConfig.Network switch
		{
			var network when network == Network.Main => PersistentConfigManager.DefaultMainNetConfig,
			var network when network == Network.TestNet => PersistentConfigManager.DefaultTestNetConfig,
			var network when network == Network.RegTest => PersistentConfigManager.DefaultRegTestConfig,
		};

		var newPersistentConfig = defaultConfig with {CoordinatorUri = CoordinatorUri};

		var newUiConfig = new UiConfig
		{
			Oobe = Oobe,
			LastVersionHighlightsDisplayed = LastVersionHighlightsDisplayed,
		};

		ApplyConfigs(newPersistentConfig, newUiConfig);
	}

	public bool IsOverridden => _config.IsOverridden;

	public IObservable<bool> IsRestartNeeded => _isRestartNeeded;

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		return _startupConfig != config;
	}

	private void Save()
	{
		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					var loadedConfig = PersistentConfigManager.LoadFile(_persistentConfigFilePath);
					if (loadedConfig is not PersistentConfig currentConfig)
					{
						throw new NotSupportedException("Only configuration files after v2.5.1 are supported.");
					}
					var newConfig = ApplyChanges(currentConfig);
					PersistentConfigManager.ToFile(_persistentConfigFilePath, newConfig);

					_isRestartNeeded.OnNext(CheckIfRestartIsNeeded(newConfig));
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	private PersistentConfig ApplyChanges(PersistentConfig config)
	{
		PersistentConfig result = config;

		// Advanced
		result = result with { EnableGpu = EnableGpu };

		// Bitcoin
		if (EndPointParser.TryParse(BitcoinRpcEndPoint, Network.DefaultPort, out EndPoint? endPoint))
		{
			result = result with { BitcoinRpcEndPoint = endPoint, BitcoinRpcCredentialString = BitcoinRpcCredentialString};
		}

		result = result with { CoordinatorUri = CoordinatorUri };
		result = result with { IndexerUri = IndexerUri };

		result = result with
		{
			Network = Network,
			UseBitcoinRpc = UseBitcoinRpc,
			DustThreshold = decimal.TryParse(DustThreshold, out var threshold) ?
				Money.Coins(threshold) :
				Money.Coins(Constants.DefaultDustThreshold),
			MaxCoinJoinMiningFeeRate = decimal.TryParse(MaxCoinJoinMiningFeeRate, out var maxCoinjoinMiningFeeRate) ?
				maxCoinjoinMiningFeeRate :
				Constants.DefaultMaxCoinJoinMiningFeeRate,
			AbsoluteMinInputCount = int.TryParse(AbsoluteMinInputCount, out var absoluteMinInputCount) ?
				absoluteMinInputCount :
				Constants.DefaultAbsoluteMinInputCount,
			ExchangeRateProvider = ExchangeRateProvider,
			FeeRateEstimationProvider = FeeRateEstimationProvider,
			ExternalTransactionBroadcaster = ExternalTransactionBroadcaster
		};

		// General
		result = result with
		{
			UseTor = UseTor.ToString(),
			TerminateTorOnExit = TerminateTorOnExit,
			DownloadNewVersion = DownloadNewVersion,
		};

		return result;
	}

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
