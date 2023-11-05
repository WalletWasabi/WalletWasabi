using Avalonia.Controls;
using NBitcoin;
using ReactiveUI;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Daemon;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class ApplicationSettings : ReactiveObject
{
	private const int ThrottleTime = 500;

	private static readonly object ConfigLock = new();

	private readonly Subject<bool> _isRestartNeeded = new();
	private readonly string _persistentConfigFilePath;
	private readonly PersistentConfig _startupConfig;
	private readonly PersistentConfig _persistentConfig;
	private readonly Config _config;
	private readonly UiConfig _uiConfig;

	// Advanced
	[AutoNotify] private bool _enableGpu;

	// Bitcoin
	[AutoNotify] private Network _network;

	[AutoNotify] private bool _startLocalBitcoinCoreOnStartup;
	[AutoNotify] private string _localBitcoinCoreDataDir;
	[AutoNotify] private bool _stopLocalBitcoinCoreOnShutdown;
	[AutoNotify] private string _bitcoinP2PEndPoint;
	[AutoNotify] private string _dustThreshold;

	// General
	[AutoNotify] private bool _darkModeEnabled;

	[AutoNotify] private bool _autoCopy;
	[AutoNotify] private bool _autoPaste;
	[AutoNotify] private bool _customChangeAddress;
	[AutoNotify] private FeeDisplayUnit _selectedFeeDisplayUnit;
	[AutoNotify] private bool _runOnSystemStartup;
	[AutoNotify] private bool _hideOnClose;
	[AutoNotify] private bool _useTor;
	[AutoNotify] private bool _terminateTorOnExit;
	[AutoNotify] private bool _downloadNewVersion;

	// Privacy Mode
	[AutoNotify] private bool _privacyMode;

	[AutoNotify] private bool _oobe;
	[AutoNotify] private WindowState _windowState;

	// Non-persistent
	[AutoNotify] private bool _doUpdateOnClose;

	public ApplicationSettings(string persistentConfigFilePath, PersistentConfig persistentConfig, Config config, UiConfig uiConfig)
	{
		_persistentConfigFilePath = persistentConfigFilePath;
		_startupConfig = new PersistentConfig(persistentConfigFilePath);
		_startupConfig.LoadFile();

		_persistentConfig = persistentConfig;
		_config = config;
		_uiConfig = uiConfig;

		// Advanced
		_enableGpu = _persistentConfig.EnableGpu;

		// Bitcoin
		_network = config.Network;
		_startLocalBitcoinCoreOnStartup = _persistentConfig.StartLocalBitcoinCoreOnStartup;
		_localBitcoinCoreDataDir = _persistentConfig.LocalBitcoinCoreDataDir;
		_stopLocalBitcoinCoreOnShutdown = _persistentConfig.StopLocalBitcoinCoreOnShutdown;
		_bitcoinP2PEndPoint = _persistentConfig.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
		_dustThreshold = _persistentConfig.DustThreshold.ToString();

		// General
		_darkModeEnabled = _uiConfig.DarkModeEnabled;
		_autoCopy = _uiConfig.Autocopy;
		_autoPaste = _uiConfig.AutoPaste;
		_customChangeAddress = _uiConfig.IsCustomChangeAddress;
		_selectedFeeDisplayUnit = Enum.IsDefined(typeof(FeeDisplayUnit), _uiConfig.FeeDisplayUnit)
			? (FeeDisplayUnit)_uiConfig.FeeDisplayUnit
			: FeeDisplayUnit.Satoshis;
		_runOnSystemStartup = _uiConfig.RunOnSystemStartup;
		_hideOnClose = _uiConfig.HideOnClose;
		_useTor = _persistentConfig.UseTor;
		_terminateTorOnExit = _persistentConfig.TerminateTorOnExit;
		_downloadNewVersion = _persistentConfig.DownloadNewVersion;

		// Privacy Mode
		_privacyMode = _uiConfig.PrivacyMode;

		_oobe = _uiConfig.Oobe;

		_windowState = (WindowState)Enum.Parse(typeof(WindowState), _uiConfig.WindowState);

		// Save on change
		this.WhenAnyValue(
			x => x.EnableGpu,
			x => x.Network,
			x => x.StartLocalBitcoinCoreOnStartup,
			x => x.LocalBitcoinCoreDataDir,
			x => x.StopLocalBitcoinCoreOnShutdown,
			x => x.BitcoinP2PEndPoint,
			x => x.DustThreshold,
			x => x.UseTor,
			x => x.TerminateTorOnExit,
			x => x.DownloadNewVersion)
			.Skip(1)
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
			x => x.SelectedFeeDisplayUnit,
			x => x.RunOnSystemStartup,
			x => x.HideOnClose,
			x => x.Oobe,
			x => x.WindowState)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => ApplyUiConfigChanges())
			.Subscribe();

		// Save UiConfig on change without throttling
		this.WhenAnyValue(
				x => x.PrivacyMode)
			.Skip(1)
			.Do(_ => ApplyUiConfigPrivacyModeChange())
			.Subscribe();

		// Set Default BitcoinCoreDataDir if required
		this.WhenAnyValue(x => x.StartLocalBitcoinCoreOnStartup)
			.Skip(1)
			.Where(value => value && string.IsNullOrEmpty(LocalBitcoinCoreDataDir))
			.Subscribe(_ => LocalBitcoinCoreDataDir = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString());

		// Apply RunOnSystemStartup
		this.WhenAnyValue(x => x.RunOnSystemStartup)
			.DoAsync(async _ => await StartupHelper.ModifyStartupSettingAsync(RunOnSystemStartup))
			.Subscribe();

		// Apply DoUpdateOnClose
		this.WhenAnyValue(x => x.DoUpdateOnClose)
			.Do(x => Services.UpdateManager.DoUpdateOnClose = x)
			.Subscribe();
	}

	public bool IsOverridden => _config.IsOverridden;

	public IObservable<bool> IsRestartNeeded => _isRestartNeeded;

	public bool CheckIfRestartIsNeeded(PersistentConfig config)
	{
		return !_startupConfig.AreDeepEqual(config);
	}

	private void Save()
	{
		var config = new PersistentConfig(_startupConfig.FilePath);

		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					lock (ConfigLock)
					{
						// TODO: Roland: do we really need to do this?
						config.LoadFile();

						ApplyChanges(config);

						config.ToFile();
					}

					_isRestartNeeded.OnNext(CheckIfRestartIsNeeded(config));
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	private void ApplyChanges(PersistentConfig config)
	{
		// Advanced
		config.EnableGpu = EnableGpu;

		// Bitcoin
		if (Network == config.Network)
		{
			if (EndPointParser.TryParse(BitcoinP2PEndPoint, Network.DefaultPort, out EndPoint? p2PEp))
			{
				config.SetBitcoinP2pEndpoint(p2PEp);
			}

			config.StartLocalBitcoinCoreOnStartup = StartLocalBitcoinCoreOnStartup;
			config.StopLocalBitcoinCoreOnShutdown = StopLocalBitcoinCoreOnShutdown;
			config.LocalBitcoinCoreDataDir = Guard.Correct(LocalBitcoinCoreDataDir);
			config.DustThreshold = decimal.TryParse(DustThreshold, out var threshold)
				? Money.Coins(threshold)
				: PersistentConfig.DefaultDustThreshold;
		}
		else
		{
			config.Network = Network;
			BitcoinP2PEndPoint = config.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
		}

		// General
		config.UseTor = UseTor;
		config.TerminateTorOnExit = TerminateTorOnExit;
		config.DownloadNewVersion = DownloadNewVersion;
	}

	private void ApplyUiConfigChanges()
	{
		_uiConfig.DarkModeEnabled = DarkModeEnabled;
		_uiConfig.Autocopy = AutoCopy;
		_uiConfig.AutoPaste = AutoPaste;
		_uiConfig.IsCustomChangeAddress = CustomChangeAddress;
		_uiConfig.FeeDisplayUnit = (int)SelectedFeeDisplayUnit;
		_uiConfig.RunOnSystemStartup = RunOnSystemStartup;
		_uiConfig.HideOnClose = HideOnClose;
		_uiConfig.Oobe = Oobe;
		_uiConfig.WindowState = WindowState.ToString();
	}

	private void ApplyUiConfigPrivacyModeChange()
	{
		_uiConfig.PrivacyMode = PrivacyMode;
	}
}
