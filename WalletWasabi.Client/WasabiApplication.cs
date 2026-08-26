using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Client;

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

		CheckVersionAndHelp();
		Directory.CreateDirectory(Config.DataDir);
		SetupLogger();
		Config = new Config(LoadOrCreateConfigs(), wasabiAppBuilder.Arguments);
		Logger.LogDebug($"Wasabi was started with these argument(s): {string.Join(" ", AppConfig.Arguments.DefaultIfEmpty("none"))}.");

		Global = new Global(Config.DataDir, Config);
		SingleInstanceChecker = new(Config.DataDir);
		TerminateService = new(TerminateApplicationAsync, AppConfig.Terminate);
	}

	private void CheckVersionAndHelp()
	{
		if (AppConfig.Arguments.Contains("--version"))
		{
			Console.WriteLine($"{AppConfig.AppName} {Constants.ClientVersion}");
			Environment.Exit((int)ExitCode.Ok);
		}

		if (AppConfig.Arguments.Contains("--help") || AppConfig.Arguments.Contains("-h"))
		{
			ShowHelp();
			Environment.Exit((int)ExitCode.Ok);
		}

	}

	public ExitCode Run(Action afterStarting)
	{
		var exitCode = ProcessAppArguments();
		if (exitCode is not null)
		{
			return exitCode.Value;
		}

		try
		{
			TerminateService.Activate();
			BeforeStarting();

			afterStarting();
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

	public async Task<ExitCode> RunAsync(Func<Task> afterStarting)
	{
		var exitCode = ProcessAppArguments();
		if (exitCode is not null)
		{
			return exitCode.Value;
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

	private ExitCode? ProcessAppArguments()
	{
		if (AppConfig.MustCheckSingleInstance)
		{
			var isFirst = SingleInstanceChecker.IsFirstInstance();

			if (!isFirst)
			{
				Logger.LogCritical($"Wasabi is already running. Please stop the other instance first.");
				return ExitCode.FailedAlreadyRunningError;
			}
		}

		return null;
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
		MigrateConfigFilesTo280();

		var networkFilePath = Path.Combine(Config.DataDir, "network");
		Logger.LogInfo($"Loading network file '{networkFilePath}'.");

		Network? network;
		var networkFileExists = File.Exists(networkFilePath);
		if (Config.GetCliArgsValue("network", AppConfig.Arguments, out var networkName))
		{
			network = Network.GetNetwork(networkName) ?? Network.Main;
			if (!networkFileExists)
			{
				PersistentConfigManager.UpdateNetwork(networkFilePath, network);
			}
		}
		else
		{
			if (networkFileExists)
			{
				networkName = File.ReadAllText(networkFilePath).Trim();
				network = Network.GetNetwork(networkName) ?? Network.Main;
			}
			else
			{
				network = Network.Main;
				PersistentConfigManager.UpdateNetwork(networkFilePath, network);
			}
		}

		var configFileName = networkName switch
		{
			_ when network == Network.Main => "Config.json",
			_ when network == Network.TestNet => "Config.TestNet.json",
			_ when network == Network.RegTest => "Config.RegTest.json",
			_ when network == Bitcoin.Instance.Signet => "Config.Signet.json",
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

	private void MigrateConfigFilesTo280()
	{
		static void Upgrade(string configFileName)
		{
			var configFilePath = Path.Combine(Config.DataDir, configFileName);
			var persistentConfig = PersistentConfigManager.LoadFile(configFilePath);

			if (persistentConfig is PersistentConfig_2_6_0 oldConfig)
			{
				var config = UpdateFrom260To280(oldConfig);
				PersistentConfigManager.ToFile(configFilePath, config);
			}
		}

		Upgrade("Config.json");
		Upgrade("Config.TestNet.json");
		Upgrade("Config.RegTest.json");
	}

	public static PersistentConfig UpdateFrom260To280(PersistentConfig_2_6_0 config)
	{
		var oldConfig = config;
		var mustMigrateMain = MustMigrate(oldConfig);
		return new PersistentConfig(
			Network: oldConfig.Network,
			AbsoluteMinInputCount: oldConfig.AbsoluteMinInputCount,
			BitcoinRpcCredentialString: mustMigrateMain ? string.Empty : oldConfig.BitcoinRpcCredentialString,
			BitcoinRpcUri: mustMigrateMain ? Constants.DefaultMainNetBitcoinRpcUri : oldConfig.BitcoinRpcUri,
			ConfigVersion: 4,
			CoordinatorUri: oldConfig.CoordinatorUri,
			CoordinatorIdentifier: oldConfig.CoordinatorIdentifier,
			DownloadNewVersion: oldConfig.DownloadNewVersion,
			DustThreshold: oldConfig.DustThreshold,
			EnableGpu: oldConfig.EnableGpu,
			ExchangeRateProvider: oldConfig.ExchangeRateProvider,
			UseTor: oldConfig.UseTor,
			FeeRateEstimationProvider: oldConfig.FeeRateEstimationProvider,
			ExternalTransactionBroadcaster: oldConfig.ExternalTransactionBroadcaster,
			JsonRpcUser: oldConfig.JsonRpcUser,
			JsonRpcPassword: oldConfig.JsonRpcPassword,
			JsonRpcServerEnabled: oldConfig.JsonRpcServerEnabled,
			JsonRpcServerPrefixes: oldConfig.JsonRpcServerPrefixes,
			TerminateTorOnExit: oldConfig.TerminateTorOnExit,
			TorBridges: oldConfig.TorBridges,
			MaxCoinJoinMiningFeeRate: oldConfig.MaxCoinJoinMiningFeeRate,
			MaxDaysInMempool: oldConfig.MaxDaysInMempool,
			ExperimentalFeatures: []
		);

		static bool MustMigrate(PersistentConfig_2_6_0 cfg) =>
			cfg.UseBitcoinRpc is false || !Uri.IsWellFormedUriString(cfg.BitcoinRpcUri, UriKind.Absolute);
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

		Logger.Configure(Path.Combine(Config.DataDir, "Logs.txt"), logLevel, Config.LogModes);
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
