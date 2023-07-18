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

public partial class ApplicationSettings : ReactiveObject, IApplicationSettings
{
	private const int ThrottleTime = 500;

	private static readonly object ConfigLock = new();

	private readonly PersistentConfig _startupConfig;
	private readonly PersistentConfig _config;
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

	public ApplicationSettings(PersistentConfig config, UiConfig uiConfig)
	{
		_startupConfig = new PersistentConfig(config.FilePath);
		_startupConfig.LoadFile();

		_config = config;
		_uiConfig = uiConfig;

		// Advanced
		_enableGpu = _config.EnableGpu;

		// Bitcoin
		_network = _config.Network;
		_startLocalBitcoinCoreOnStartup = _config.StartLocalBitcoinCoreOnStartup;
		_localBitcoinCoreDataDir = _config.LocalBitcoinCoreDataDir;
		_stopLocalBitcoinCoreOnShutdown = _config.StopLocalBitcoinCoreOnShutdown;
		_bitcoinP2PEndPoint = _config.GetBitcoinP2pEndPoint().ToString(defaultPort: -1);
		_dustThreshold = _config.DustThreshold.ToString();

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
		_useTor = _config.UseTor;
		_terminateTorOnExit = _config.TerminateTorOnExit;
		_downloadNewVersion = _config.DownloadNewVersion;

		// Privacy Mode
		_privacyMode = _uiConfig.PrivacyMode;

		// Save on change
		this.WhenAnyValue(
			x => x.EnableGpu,
			x => x.Network,
			x => x.StartLocalBitcoinCoreOnStartup,
			x => x.LocalBitcoinCoreDataDir,
			x => x.StopLocalBitcoinCoreOnShutdown,
			x => x.BitcoinP2PEndPoint,
			x => x.DustThreshold)
			.Skip(1)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => Save())
			.Subscribe();

		// Save on change (continued)
		this.WhenAnyValue(
			x => x.UseTor,
			x => x.TerminateTorOnExit,
			x => x.DownloadNewVersion)
			.Skip(1)
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
			x => x.HideOnClose)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Do(_ => ApplyUiConfigChanges())
			.Subscribe();

		// Set Default BitcoinCoreDataDir if required
		this.WhenAnyValue(x => x.StartLocalBitcoinCoreOnStartup)
			.Skip(1)
			.Where(value => value && string.IsNullOrEmpty(LocalBitcoinCoreDataDir))
			.Subscribe(_ => LocalBitcoinCoreDataDir = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString());

		// Apply RunOnSystenStartup
		this.WhenAnyValue(x => x.RunOnSystemStartup)
			.DoAsync(async x => await StartupHelper.ModifyStartupSettingAsync(RunOnSystemStartup))
			.Subscribe();
	}

	private Subject<bool> _isRestartNeeded = new();

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

						_isRestartNeeded.OnNext(CheckIfRestartIsNeeded(config));
					}
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
	}
}
