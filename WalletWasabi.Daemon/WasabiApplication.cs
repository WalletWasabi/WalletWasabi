using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Daemon;

public class WasabiApplication
{
	public WasabiAppBuilder AppConfig { get; }
	public Global Global { get; }
	public Config Config { get; }
	public SingleInstanceChecker SingleInstanceChecker { get; }
	public TerminateService TerminateService { get; }
	private static Guid InstanceGuid { get; } = Guid.NewGuid();

	public WasabiApplication(WasabiAppBuilder wasabiAppBuilder)
	{
		AppConfig = wasabiAppBuilder;

		Directory.CreateDirectory(Config.DataDir);
		Config = new Config(LoadOrCreateConfigs(), wasabiAppBuilder.Arguments);
		SetupLogger();
		Logger.LogDebug($"Wasabi was started with these argument(s): {string.Join(" ", AppConfig.Arguments.DefaultIfEmpty("none"))}.");

		Global = new Global(Config.DataDir, Config);
		SingleInstanceChecker = new(Config.Network);
		TerminateService = new(TerminateApplicationAsync, AppConfig.Terminate);
	}

	public async Task<ExitCode> RunAsync(Func<Task> afterStarting)
	{
		if (AppConfig.Arguments.Contains("--version"))
		{
			Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
			return ExitCode.Ok;
		}
		if (AppConfig.Arguments.Contains("--help") || AppConfig.Arguments.Contains("-h"))
		{
			ShowHelp();
			return ExitCode.Ok;
		}

		if (AppConfig.MustCheckSingleInstance)
		{
			var instanceResult = await SingleInstanceChecker.CheckSingleInstanceAsync();
			if (instanceResult == WasabiInstanceStatus.AnotherInstanceIsRunning)
			{
				Logger.LogDebug("Wasabi is already running, signaled the first instance.");
				return ExitCode.FailedAlreadyRunningSignaled;
			}
			if (instanceResult == WasabiInstanceStatus.Error)
			{
				Logger.LogCritical($"Wasabi is already running, but cannot be signaled");
				return ExitCode.FailedAlreadyRunningError;
			}
		}

		try
		{
			TerminateService.Activate();

			BeforeStarting();

			await afterStarting();
			return ExitCode.Ok;
		}
		catch (Exception e)
		{
			Logger.LogInfo("Exception occurred while the application was starting or running", e);
			throw;
		}
		finally
		{
			BeforeStopping();
		}
	}

	private void BeforeStarting()
	{
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		Logger.LogInfo($"{AppConfig.AppName} started ({InstanceGuid}).", callerFilePath: "", callerLineNumber: -1);
	}

	private void BeforeStopping()
	{
		AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;

		// Start termination/disposal of the application.
		TerminateService.Terminate();
		SingleInstanceChecker.Dispose();
		Logger.LogInfo($"{AppConfig.AppName} stopped gracefully ({InstanceGuid}).", callerFilePath: "", callerLineNumber: -1);
	}

	private PersistentConfig LoadOrCreateConfigs()
	{
		CreateConfigFiles();
		MigrateConfigFiles();

		var networkFilePath = Path.Combine(Config.DataDir, "network");
		Config.GetCliArgsValue("network", AppConfig.Arguments, out var networkName);
		networkName ??= File.ReadAllText(networkFilePath).Trim();
		var network = Network.GetNetwork(networkName ?? "mainnet");
		var configFileName = networkName switch
		{
			_ when network == Network.Main => "Config.json",
			_ when network == Network.TestNet =>  "Config.TestNet.json",
			_ when network == Network.RegTest =>  "Config.RegTest.json",
			_ when network == Bitcoin.Instance.Signet =>  "Config.Signet.json",
			_ => throw new NotSupportedException($"Network '{networkName}' is not supported."),
		};
		var configFilePath = Path.Combine(Config.DataDir, configFileName);

		Logger.LogInfo($"Loading config file '{configFilePath}'.");
		var persistentConfig = PersistentConfigManager.LoadFile(configFilePath);

		if (persistentConfig is PersistentConfig config)
		{
			var configForNetwork = config with { Network = network };
			return configForNetwork;
		}

		throw new InvalidOperationException("Unknown configuration type");
	}

	private void CreateConfigFiles()
	{
		CreateConfigFileIfNotExists(Path.Combine(Config.DataDir, "Config.RegTest.json"),
			PersistentConfigManager.DefaultRegTestConfig);
		CreateConfigFileIfNotExists(Path.Combine(Config.DataDir, "Config.TestNet.json"),
			PersistentConfigManager.DefaultTestNetConfig);
		CreateConfigFileIfNotExists(Path.Combine(Config.DataDir, "Config.Signet.json"),
			PersistentConfigManager.DefaultSignetConfig);
		CreateConfigFileIfNotExists(Path.Combine(Config.DataDir, "Config.json"),
			PersistentConfigManager.DefaultMainNetConfig);
		return;

		static void CreateConfigFileIfNotExists(string filePath, PersistentConfig config)
		{
			if (!File.Exists(filePath))
			{
				PersistentConfigManager.ToFile(filePath, config);
			}
		}
	}

	private void MigrateConfigFiles()
	{
		var configFilePath = Path.Combine(Config.DataDir, "Config.json");
		var persistentConfig = PersistentConfigManager.LoadFile(configFilePath);

		if (persistentConfig is PersistentConfigPrev2_6_0 oldConfig)
		{
			oldConfig = oldConfig.Migrate();
			var mainConfig = new PersistentConfig(
				Network: Network.Main,
				AbsoluteMinInputCount : oldConfig.AbsoluteMinInputCount,
				BitcoinRpcCredentialString : oldConfig.MainNetBitcoinRpcCredentialString,
				BitcoinRpcUri : oldConfig.MainNetBitcoinRpcEndPoint.ToUriString("http"),
				ConfigVersion : 3,
				CoordinatorUri : oldConfig.MainNetCoordinatorUri,
				CoordinatorIdentifier : oldConfig.CoordinatorIdentifier,
				DownloadNewVersion : oldConfig.DownloadNewVersion,
				DustThreshold : oldConfig.DustThreshold,
				EnableGpu : oldConfig.EnableGpu,
				ExchangeRateProvider : oldConfig.ExchangeRateProvider,
				UseTor : oldConfig.UseTor,
				FeeRateEstimationProvider : oldConfig.FeeRateEstimationProvider,
				ExternalTransactionBroadcaster : oldConfig.ExternalTransactionBroadcaster,
				UseBitcoinRpc : oldConfig.UseBitcoinRpc,
				JsonRpcUser : oldConfig.JsonRpcUser,
				JsonRpcPassword : oldConfig.JsonRpcPassword,
				JsonRpcServerEnabled : oldConfig.JsonRpcServerEnabled,
				JsonRpcServerPrefixes : new ValueList<string>(oldConfig.JsonRpcServerPrefixes),
				TerminateTorOnExit : oldConfig.TerminateTorOnExit,
				IndexerUri : oldConfig.MainNetIndexerUri,
				TorBridges : new ValueList<string>(oldConfig.TorBridges),
				MaxCoinJoinMiningFeeRate : oldConfig.MaxCoinJoinMiningFeeRate,
				MaxDaysInMempool: oldConfig.MaxDaysInMempool,
				ExperimentalFeatures: ValueList<string>.Empty
			);

			var testConfig = mainConfig with
			{
				Network	= Network.TestNet,
				IndexerUri = oldConfig.TestNetIndexerUri,
				CoordinatorUri = oldConfig.TestNetCoordinatorUri,
				BitcoinRpcCredentialString = oldConfig.TestNetBitcoinRpcCredentialString,
				BitcoinRpcUri = oldConfig.TestNetBitcoinRpcEndPoint.ToUriString("http"),
				ExperimentalFeatures = new ValueList<string>(["scripting"])
			};

			var regtestConfig = mainConfig with
			{
				Network = Network.RegTest,
				IndexerUri = oldConfig.RegTestIndexerUri,
				CoordinatorUri = oldConfig.RegTestCoordinatorUri,
				BitcoinRpcCredentialString = oldConfig.RegTestBitcoinRpcCredentialString,
				BitcoinRpcUri = oldConfig.RegTestBitcoinRpcEndPoint.ToUriString("http"),
				ExperimentalFeatures = new ValueList<string>(["scripting"])
			};

			var regtestConfigFilePath = Path.Combine(Config.DataDir, "Config.RegTest.json");
			PersistentConfigManager.ToFile(regtestConfigFilePath, regtestConfig);

			var testnetConfigFilePath = Path.Combine(Config.DataDir, "Config.TestNet.json");
			PersistentConfigManager.ToFile(testnetConfigFilePath, testConfig);

			var mainnetConfigFilePath = Path.Combine(Config.DataDir, "Config.json");
			PersistentConfigManager.ToFile(mainnetConfigFilePath, mainConfig);
		}
	}

	private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		AppConfig.UnobservedTaskExceptionsEventHandler?.Invoke(this, e.Exception);
	}

	private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			AppConfig.UnhandledExceptionEventHandler?.Invoke(this, ex);
		}
	}

	private async Task TerminateApplicationAsync()
	{
		Logger.LogInfo($"{AppConfig.AppName} stopped gracefully ({InstanceGuid}).", callerFilePath: "", callerLineNumber: -1);

		await Global.DisposeAsync().ConfigureAwait(false);
	}

	private void SetupLogger()
	{
		LogLevel logLevel = Enum.TryParse(Config.LogLevel, ignoreCase: true, out LogLevel parsedLevel)
			? parsedLevel
			: LogLevel.Info;

		Logger.InitializeDefaults(Path.Combine(Config.DataDir, "Logs.txt"), logLevel, Config.LogModes);
	}

	private void ShowHelp()
	{
		Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
		Console.WriteLine($"Usage: {AppConfig.AppName} [OPTION]...");
		Console.WriteLine();
		Console.WriteLine("Available options are:");

		foreach (var (parameter, hint) in Config.GetConfigOptionsMetadata().OrderBy(x => x.ParameterName))
		{
			Console.Write($"  --{parameter.ToLower(),-30} ");
			var hintLines = hint.SplitLines(lineWidth: 40);
			Console.WriteLine(hintLines[0]);
			foreach (var hintLine in hintLines.Skip(1))
			{
				Console.WriteLine($"{' ',-35}{hintLine}");
			}
			Console.WriteLine();
		}
	}
}
