using NBitcoin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Daemon;

public class Config
{
	public static readonly IDictionary EnvironmentVariables = Environment.GetEnvironmentVariables();

	public Config(PersistentConfig persistentConfig, string[] cliArgs)
	{
		PersistentConfig = persistentConfig;
		CliArgs = cliArgs;

		LogMode[] defaultLogModes;

#if RELEASE
		defaultLogModes = [LogMode.Console, LogMode.File];
#else
		defaultLogModes = [LogMode.Debug, LogMode.Console, LogMode.File];
#endif

		Data = new()
		{
			[ nameof(Network)] = (
				Lang.Resources.CLI_NetworkSettingsHint,
				GetNetworkValue("Network", PersistentConfig.Network.ToString(), cliArgs)),
			[ nameof(MainNetBackendUri)] = (
				Lang.Resources.CLI_MainNetBackendUriSettingsHint,
				GetStringValue("MainNetBackendUri", PersistentConfig.MainNetBackendUri, cliArgs)),
			[ nameof(TestNetBackendUri)] = (
				Lang.Resources.CLI_TestNetBackendUriSettingsHint,
				GetStringValue("TestNetBackendUri", PersistentConfig.TestNetBackendUri, cliArgs)),
			[ nameof(RegTestBackendUri)] = (
				Lang.Resources.CLI_RegTestBackendUriSettingsHint,
				GetStringValue("RegTestBackendUri", PersistentConfig.RegTestBackendUri, cliArgs)),
			[ nameof(MainNetCoordinatorUri)] = (
				Lang.Resources.CLI_MainNetCoordinatorUriSettingsHint,
				GetStringValue("MainNetCoordinatorUri", PersistentConfig.MainNetCoordinatorUri, cliArgs)),
			[ nameof(TestNetCoordinatorUri)] = (
				Lang.Resources.CLI_TestNetCoordinatorUriSettingsHint,
				GetStringValue("TestNetCoordinatorUri", PersistentConfig.TestNetCoordinatorUri, cliArgs)),
			[ nameof(RegTestCoordinatorUri)] = (
				Lang.Resources.CLI_RegTestCoordinatorUriSettingsHint,
				GetStringValue("RegTestCoordinatorUri", PersistentConfig.RegTestCoordinatorUri, cliArgs)),
			[ nameof(UseTor)] = (
				Lang.Resources.CLI_UseTorSettingsHint,
				GetTorModeValue("UseTor", PersistentConfig.UseTor, cliArgs)),
			[ nameof(TorFolder)] = (
				Lang.Resources.CLI_TorFolderSettingsHint,
				GetNullableStringValue("TorFolder", null, cliArgs)),
			[ nameof(TorSocksPort)] = (
				Lang.Resources.CLI_TorSocksPortSettingsHint,
				GetLongValue("TorSocksPort", TorSettings.DefaultSocksPort, cliArgs)),
			[ nameof(TorControlPort)] = (
				Lang.Resources.CLI_TorControlPortSettingsHint,
				GetLongValue("TorControlPort", TorSettings.DefaultControlPort, cliArgs)),
			[nameof(TorBridges)] = (
				Lang.Resources.CLI_TorBridgesSettingsHint,
				GetStringArrayValue("TorBridges", PersistentConfig.TorBridges, cliArgs)),
			[nameof(TerminateTorOnExit)] = (
				Lang.Resources.CLI_TerminateTorOnExitSettingsHint,
				GetBoolValue("TerminateTorOnExit", PersistentConfig.TerminateTorOnExit, cliArgs)),
			[ nameof(DownloadNewVersion)] = (
				Lang.Resources.CLI_DownloadNewVersionSettingsHint,
				GetBoolValue("DownloadNewVersion", PersistentConfig.DownloadNewVersion, cliArgs)),
			[ nameof(StartLocalBitcoinCoreOnStartup)] = (
				Lang.Resources.CLI_StartLocalBitcoinCoreOnStartupSettingsHint,
				GetBoolValue("StartLocalBitcoinCoreOnStartup", PersistentConfig.StartLocalBitcoinCoreOnStartup, cliArgs)),
			[ nameof(StopLocalBitcoinCoreOnShutdown)] = (
				Lang.Resources.CLI_StopLocalBitcoinCoreOnShutdownSettingsHint,
				GetBoolValue("StopLocalBitcoinCoreOnShutdown", PersistentConfig.StopLocalBitcoinCoreOnShutdown, cliArgs)),
			[ nameof(LocalBitcoinCoreDataDir)] = (
				Lang.Resources.CLI_LocalBitcoinCoreDataDirSettingsHint,
				GetStringValue("LocalBitcoinCoreDataDir", PersistentConfig.LocalBitcoinCoreDataDir, cliArgs)),
			[ nameof(MainNetBitcoinP2pEndPoint)] = (
				"-",
				GetEndPointValue("MainNetBitcoinP2pEndPoint", PersistentConfig.MainNetBitcoinP2pEndPoint, cliArgs)),
			[ nameof(TestNetBitcoinP2pEndPoint)] = (
				"-",
				GetEndPointValue("TestNetBitcoinP2pEndPoint", PersistentConfig.TestNetBitcoinP2pEndPoint, cliArgs)),
			[ nameof(RegTestBitcoinP2pEndPoint)] = (
				"-",
				GetEndPointValue("RegTestBitcoinP2pEndPoint", PersistentConfig.RegTestBitcoinP2pEndPoint, cliArgs)),
			[ nameof(JsonRpcServerEnabled)] = (
				Lang.Resources.CLI_JsonRpcServerEnabledSettingsHint,
				GetBoolValue("JsonRpcServerEnabled", PersistentConfig.JsonRpcServerEnabled, cliArgs)),
			[ nameof(JsonRpcUser)] = (
				Lang.Resources.CLI_JsonRpcUserSettingsHint,
				GetStringValue("JsonRpcUser", PersistentConfig.JsonRpcUser, cliArgs)),
			[ nameof(JsonRpcPassword)] = (
				Lang.Resources.CLI_JsonRpcPasswordSettingsHint,
				GetStringValue("JsonRpcPassword", PersistentConfig.JsonRpcPassword, cliArgs)),
			[ nameof(JsonRpcServerPrefixes)] = (
				Lang.Resources.CLI_JsonRpcServerPrefixesSettingsHint,
				GetStringArrayValue("JsonRpcServerPrefixes", PersistentConfig.JsonRpcServerPrefixes, cliArgs)),
			[ nameof(RpcOnionEnabled)] = (
				Lang.Resources.CLI_RpcOnionEnabledSettingsHint,
				GetBoolValue("RpcOnionEnabled", value: false, cliArgs)),
			[ nameof(DustThreshold)] = (
				Lang.Resources.CLI_DustThresholdSettingsHint,
				GetMoneyValue("DustThreshold", PersistentConfig.DustThreshold, cliArgs)),
			[ nameof(BlockOnlyMode)] = (
				Lang.Resources.CLI_BlockOnlyModeSettingsHint,
				GetBoolValue("BlockOnly", value: false, cliArgs)),
			[ nameof(LogLevel)] = (
				Lang.Resources.CLI_LogLevelSettingsHint,
				GetStringValue("LogLevel", value: "", cliArgs)),
			[ nameof(LogModes)] = (
				Lang.Resources.CLI_LogModesSettingsHint,
				GetLogModeArrayValue("LogModes", arrayValues: defaultLogModes, cliArgs)),
			[ nameof(EnableGpu)] = (
				Lang.Resources.CLI_EnableGpuSettingsHint,
				GetBoolValue("EnableGpu", PersistentConfig.EnableGpu, cliArgs)),
			[ nameof(CoordinatorIdentifier)] = (
				"-",
				GetStringValue("CoordinatorIdentifier", PersistentConfig.CoordinatorIdentifier, cliArgs)),
			[ nameof(MaxCoinjoinMiningFeeRate)] = (
				Lang.Resources.CLI_MaxCoinjoinMiningFeeRateSettingsHint,
				GetDecimalValue("MaxCoinjoinMiningFeeRate", PersistentConfig.MaxCoinJoinMiningFeeRate, cliArgs)),
			[ nameof(AbsoluteMinInputCount)] = (
				Lang.Resources.CLI_AbsoluteMinInputCountSettingsHint,
				GetLongValue("AbsoluteMinInputCount", PersistentConfig.AbsoluteMinInputCount, cliArgs)),
			[nameof(Language)] = (
				Lang.Resources.CLI_LanguageSettingsHint,
				GetLongValue("Language", PersistentConfig.DisplayLanguage, cliArgs)),
		};

		// Check if any config value is overridden (either by an environment value, or by a CLI argument).
		foreach (string optionName in Data.Keys)
		{
			// It is allowed to override the log level.
			if (!string.Equals(optionName, nameof(LogLevel)))
			{
				(_, IValue optionValue) = Data[optionName];

				if (optionValue.Overridden)
				{
					IsOverridden = true;
					break;
				}
			}
		}

		ServiceConfiguration = new ServiceConfiguration(GetBitcoinP2pEndPoint(), DustThreshold);
	}

	private Dictionary<string, (string Hint, IValue Value)> Data { get; }
	public PersistentConfig PersistentConfig { get; }
	public string[] CliArgs { get; }
	public Network Network => GetEffectiveValue<NetworkValue, Network>(nameof(Network));

	public string MainNetBackendUri => GetEffectiveValue<StringValue, string>(nameof(MainNetBackendUri));
	public string TestNetBackendUri => GetEffectiveValue<StringValue, string>(nameof(TestNetBackendUri));
	public string RegTestBackendUri => GetEffectiveValue<StringValue, string>(nameof(RegTestBackendUri));
	public string MainNetCoordinatorUri => GetEffectiveValue<StringValue, string>(nameof(MainNetCoordinatorUri));
	public string TestNetCoordinatorUri => GetEffectiveValue<StringValue, string>(nameof(TestNetCoordinatorUri));
	public string RegTestCoordinatorUri => GetEffectiveValue<StringValue, string>(nameof(RegTestCoordinatorUri));
	public TorMode UseTor => Network == Network.RegTest ? TorMode.Disabled : GetEffectiveValue<TorModeValue, TorMode>(nameof(UseTor));
	public string? TorFolder => GetEffectiveValue<NullableStringValue, string?>(nameof(TorFolder));
	public int TorSocksPort => GetEffectiveValue<IntValue, int>(nameof(TorSocksPort));
	public int TorControlPort => GetEffectiveValue<IntValue, int>(nameof(TorControlPort));
	public string[] TorBridges => GetEffectiveValue<StringArrayValue, string[]>(nameof(TorBridges));
	public bool TerminateTorOnExit => GetEffectiveValue<BoolValue, bool>(nameof(TerminateTorOnExit));
	public bool DownloadNewVersion => GetEffectiveValue<BoolValue, bool>(nameof(DownloadNewVersion));
	public bool StartLocalBitcoinCoreOnStartup => GetEffectiveValue<BoolValue, bool>(nameof(StartLocalBitcoinCoreOnStartup));
	public bool StopLocalBitcoinCoreOnShutdown => GetEffectiveValue<BoolValue, bool>(nameof(StopLocalBitcoinCoreOnShutdown));
	public string LocalBitcoinCoreDataDir => GetEffectiveValue<StringValue, string>(nameof(LocalBitcoinCoreDataDir));
	public EndPoint MainNetBitcoinP2pEndPoint => GetEffectiveValue<EndPointValue, EndPoint>(nameof(MainNetBitcoinP2pEndPoint));
	public EndPoint TestNetBitcoinP2pEndPoint => GetEffectiveValue<EndPointValue, EndPoint>(nameof(TestNetBitcoinP2pEndPoint));
	public EndPoint RegTestBitcoinP2pEndPoint => GetEffectiveValue<EndPointValue, EndPoint>(nameof(RegTestBitcoinP2pEndPoint));
	public bool JsonRpcServerEnabled => GetEffectiveValue<BoolValue, bool>(nameof(JsonRpcServerEnabled));
	public string JsonRpcUser => GetEffectiveValue<StringValue, string>(nameof(JsonRpcUser));
	public string JsonRpcPassword => GetEffectiveValue<StringValue, string>(nameof(JsonRpcPassword));
	public string[] JsonRpcServerPrefixes => GetEffectiveValue<StringArrayValue, string[]>(nameof(JsonRpcServerPrefixes));
	public bool RpcOnionEnabled => GetEffectiveValue<BoolValue, bool>(nameof(RpcOnionEnabled));
	public Money DustThreshold => GetEffectiveValue<MoneyValue, Money>(nameof(DustThreshold));
	public bool BlockOnlyMode => GetEffectiveValue<BoolValue, bool>(nameof(BlockOnlyMode));
	public string LogLevel => GetEffectiveValue<StringValue, string>(nameof(LogLevel));
	public LogMode[] LogModes => GetEffectiveValue<LogModeArrayValue, LogMode[]>(nameof(LogModes));

	public bool EnableGpu => GetEffectiveValue<BoolValue, bool>(nameof(EnableGpu));
	public string CoordinatorIdentifier => GetEffectiveValue<StringValue, string>(nameof(CoordinatorIdentifier));
	public decimal MaxCoinjoinMiningFeeRate => GetEffectiveValue<DecimalValue, decimal>(nameof(MaxCoinjoinMiningFeeRate));
	public int AbsoluteMinInputCount => int.Max(
		GetEffectiveValue<IntValue, int>(nameof(AbsoluteMinInputCount)),
		Constants.AbsoluteMinInputCount);
	public int Language => GetEffectiveValue<IntValue, int>(nameof(Language));

	public ServiceConfiguration ServiceConfiguration { get; }

	public static string DataDir { get; } = GetStringValue(
		"datadir",
		EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")),
		Environment.GetCommandLineArgs()).EffectiveValue;

	/// <summary>Whether a config option was overridden by a command line argument or an environment variable.</summary>
	/// <remarks>
	/// Changing config options in the UI while a config option is overridden would bring uncertainty if user understands consequences or not,
	/// thus it is normally not allowed. However, there are exceptions as what options are taken into account, there is currently
	/// one exception: <see cref="LogLevel"/>.
	/// </remarks>
	public bool IsOverridden { get; }

	public EndPoint GetBitcoinP2pEndPoint()
	{
		if (Network == Network.Main)
		{
			return MainNetBitcoinP2pEndPoint;
		}

		if (Network == Network.TestNet)
		{
			return TestNetBitcoinP2pEndPoint;
		}

		if (Network == Network.RegTest)
		{
			return RegTestBitcoinP2pEndPoint;
		}

		throw new NotSupportedNetworkException(Network);
	}

	public Uri GetBackendUri()
	{
		if (Network == Network.Main)
		{
			return new Uri(MainNetBackendUri);
		}

		if (Network == Network.TestNet)
		{
			return new Uri(TestNetBackendUri);
		}

		if (Network == Network.RegTest)
		{
			return new Uri(RegTestBackendUri);
		}

		throw new NotSupportedNetworkException(Network);
	}

	private Uri GetCoordinatorUri()
	{
		var result = Network switch
		{
			{ } n when n == Network.Main => MainNetCoordinatorUri,
			{ } n when n == Network.TestNet => TestNetCoordinatorUri,
			{ } n when n == Network.RegTest => RegTestCoordinatorUri,
			_ => throw new NotSupportedNetworkException(Network)
		};

		return new Uri(result);
	}

	public bool TryGetCoordinatorUri([NotNullWhen(true)] out Uri? coordinatorUri)
	{
		try
		{
			coordinatorUri = GetCoordinatorUri();
			return coordinatorUri.Host != "api.wasabiwallet.io";
		}
		catch (Exception e) when (e is UriFormatException or ArgumentException or NotSupportedNetworkException)
		{
			coordinatorUri = null;
			return false;
		}
	}

	public IEnumerable<(string ParameterName, string Hint)> GetConfigOptionsMetadata() =>
		Data.Select(x => (x.Key, x.Value.Hint));

	private EndPointValue GetEndPointValue(string key, EndPoint value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			if (!EndPointParser.TryParse(overrideValue, 0, out var endpoint))
			{
				throw new ArgumentNullException(key, "Not a valid endpoint");
			}

			return new EndPointValue(value, endpoint, valueSource.Value);
		}

		return new EndPointValue(value, value, ValueSource.Disk);
	}

	private MoneyValue GetMoneyValue(string key, Money value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			if (!Money.TryParse(overrideValue, out var money))
			{
				throw new ArgumentNullException("DustThreshold", "Not a valid money");
			}

			return new MoneyValue(value, money, valueSource.Value);
		}

		return new MoneyValue(value, value, ValueSource.Disk);
	}

	private NetworkValue GetNetworkValue(string key, string value, string[] cliArgs)
	{
		StringValue stringValue = GetStringValue(key, value, cliArgs);

		return new NetworkValue(
			Value: Network.GetNetwork(stringValue.Value) ?? throw new ArgumentException("Network", $"Unknown network '{stringValue.Value}'"),
			EffectiveValue: Network.GetNetwork(stringValue.EffectiveValue) ?? throw new ArgumentException("Network", $"Unknown network '{stringValue.EffectiveValue}'"),
			stringValue.ValueSource);
	}

	private BoolValue GetBoolValue(string key, bool value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			if (!bool.TryParse(overrideValue, out bool argsBoolValue))
			{
				throw new ArgumentException("must be 'true' or 'false'.", key);
			}

			return new BoolValue(value, argsBoolValue, valueSource.Value);
		}

		return new BoolValue(value, value, ValueSource.Disk);
	}

	private DecimalValue GetDecimalValue(string key, decimal value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			if (!int.TryParse(overrideValue, out int argsLongValue))
			{
				throw new ArgumentException("must be a decimal number.", key);
			}

			return new DecimalValue(value, argsLongValue, valueSource.Value);
		}

		return new DecimalValue(value, value, ValueSource.Disk);
	}

	private IntValue GetLongValue(string key, int value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			if (!int.TryParse(overrideValue, out int argsLongValue))
			{
				throw new ArgumentException("must be a number.", key);
			}

			return new IntValue(value, argsLongValue, valueSource.Value);
		}

		return new IntValue(value, value, ValueSource.Disk);
	}

	private static StringValue GetStringValue(string key, string value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			return new StringValue(value, overrideValue, valueSource.Value);
		}

		return new StringValue(value, value, ValueSource.Disk);
	}

	private static NullableStringValue GetNullableStringValue(string key, string? value, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			return new NullableStringValue(value, overrideValue, valueSource.Value);
		}

		return new NullableStringValue(value, value, ValueSource.Disk);
	}

	private static StringArrayValue GetStringArrayValue(string key, string[] arrayValues, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			string[] overrideValues = overrideValue.Split(';', StringSplitOptions.None);
			return new StringArrayValue(arrayValues, overrideValues, valueSource.Value);
		}

		return new StringArrayValue(arrayValues, arrayValues, ValueSource.Disk);
	}

	private static LogModeArrayValue GetLogModeArrayValue(string key, LogMode[] arrayValues, string[] cliArgs)
	{
		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			LogMode[] logModes = overrideValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Where(x => !string.IsNullOrWhiteSpace(x)) // Filter our whitespace-only elements.
				.Select(x =>
				{
					if (!Enum.TryParse(x.Trim(), ignoreCase: true, out LogMode logMode))
					{
						throw new NotSupportedException($"Logging mode '{x}' is not supported.");
					}

					return logMode;
				})
				.ToHashSet() // Remove duplicates.
				.ToArray();

			return new LogModeArrayValue(arrayValues, logModes, valueSource.Value);
		}

		return new LogModeArrayValue(arrayValues, arrayValues, ValueSource.Disk);
	}

	private static TorModeValue GetTorModeValue(string key, object value, string[] cliArgs)
	{
		TorMode computedValue;

		computedValue = ObjectToTorMode(value);

		if (GetOverrideValue(key, cliArgs, out string? overrideValue, out ValueSource? valueSource))
		{
			TorMode parsedOverrideValue = ObjectToTorMode(overrideValue);
			return new TorModeValue(computedValue, parsedOverrideValue, valueSource.Value);
		}

		return new TorModeValue(computedValue, computedValue, ValueSource.Disk);
	}

	public static TorMode ObjectToTorMode(object value)
	{
		string? stringValue = value.ToString();

		TorMode computedValue;
		if (stringValue is null)
		{
			throw new ArgumentException($"Could not convert '{value}' to a string value.");
		}
		else if (stringValue.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			computedValue = TorMode.Enabled;
		}
		else if (stringValue.Equals("false", StringComparison.OrdinalIgnoreCase))
		{
			computedValue = TorMode.Disabled;
		}
		else if (Enum.TryParse(stringValue, ignoreCase: true, out TorMode parsedTorMode))
		{
			computedValue = parsedTorMode;
		}
		else
		{
			throw new ArgumentException($"Could not convert '{value}' to a valid {nameof(TorMode)} value.");
		}

		return computedValue;
	}

	private static bool GetOverrideValue(string key, string[] cliArgs, [NotNullWhen(true)] out string? overrideValue, [NotNullWhen(true)] out ValueSource? valueSource)
	{
		// CLI arguments have higher precedence than environment variables.
		if (GetCliArgsValue(key, cliArgs, out string? argsValue))
		{
			valueSource = ValueSource.CommandLineArgument;
			overrideValue = argsValue;
			return true;
		}

		if (GetEnvironmentVariable(key, out string? envVarValue))
		{
			valueSource = ValueSource.EnvironmentVariable;
			overrideValue = envVarValue;
			return true;
		}

		valueSource = null;
		overrideValue = null;
		return false;
	}

	private static bool GetCliArgsValue(string key, string[] cliArgs, [NotNullWhen(true)] out string? cliArgsValue)
	{
		if (ArgumentHelpers.TryGetValue(key, cliArgs, out cliArgsValue))
		{
			return true;
		}

		cliArgsValue = null;
		return false;
	}

	private static bool GetEnvironmentVariable(string key, [NotNullWhen(true)] out string? envValue)
	{
		string envKey = $"WASABI-{key.ToUpperInvariant()}";

		if (EnvironmentVariables.Contains(envKey))
		{
			if (EnvironmentVariables[envKey] is string envVar)
			{
				envValue = envVar;
				return true;
			}
		}

		envValue = null;
		return false;
	}

	private TValue GetEffectiveValue<TStorage, TValue>(string key) where TStorage : ITypedValue<TValue>
	{
		if (Data.TryGetValue(key, out (string, IValue value) valueObject) && valueObject.value is ITypedValue<TValue> typedValue)
		{
			return typedValue.EffectiveValue;
		}

		throw new InvalidOperationException($"Failed to find key '{key}' in config storage.");
	}

	/// <summary>Source of application config value.</summary>
	private enum ValueSource
	{
		/// <summary>Value stored in JSON config on disk.</summary>
		Disk,

		/// <summary>CLI argument passed by user to override disk config value.</summary>
		CommandLineArgument,

		/// <summary>Environment variable overriding disk config value.</summary>
		EnvironmentVariable
	}

	private interface IValue
	{
		ValueSource ValueSource { get; }
		bool Overridden => ValueSource != ValueSource.Disk;
	}

	private interface ITypedValue<T> : IValue
	{
		T Value { get; }
		T EffectiveValue { get; }
	}

	private record BoolValue(bool Value, bool EffectiveValue, ValueSource ValueSource) : ITypedValue<bool>;
	private record IntValue(int Value, int EffectiveValue, ValueSource ValueSource) : ITypedValue<int>;
	private record DecimalValue(decimal Value, decimal EffectiveValue, ValueSource ValueSource) : ITypedValue<decimal>;
	private record StringValue(string Value, string EffectiveValue, ValueSource ValueSource) : ITypedValue<string>;
	private record NullableStringValue(string? Value, string? EffectiveValue, ValueSource ValueSource) : ITypedValue<string?>;
	private record StringArrayValue(string[] Value, string[] EffectiveValue, ValueSource ValueSource) : ITypedValue<string[]>;
	private record LogModeArrayValue(LogMode[] Value, LogMode[] EffectiveValue, ValueSource ValueSource) : ITypedValue<LogMode[]>;
	private record TorModeValue(TorMode Value, TorMode EffectiveValue, ValueSource ValueSource) : ITypedValue<TorMode>;
	private record NetworkValue(Network Value, Network EffectiveValue, ValueSource ValueSource) : ITypedValue<Network>;
	private record MoneyValue(Money Value, Money EffectiveValue, ValueSource ValueSource) : ITypedValue<Money>;
	private record EndPointValue(EndPoint Value, EndPoint EffectiveValue, ValueSource ValueSource) : ITypedValue<EndPoint>;
}
