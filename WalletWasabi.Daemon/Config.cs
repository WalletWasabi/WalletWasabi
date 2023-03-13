using System;
using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using WalletWasabi.Bases;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Daemon;

[JsonObject(MemberSerialization.OptIn)]
public class Config : ConfigBase
{
	public const int DefaultJsonRpcServerPort = 37128;
	public static readonly Money DefaultDustThreshold = Money.Coins(Constants.DefaultDustThreshold);

	/// <summary>
	/// Constructor for config population using Newtonsoft.JSON.
	/// </summary>
	public Config() : base()
	{
	}

	public Config(string filePath) : base(filePath)
	{
	}

	[JsonProperty(PropertyName = "Network")]
	[JsonConverter(typeof(NetworkJsonConverter))]
	public Network Network { get; set; } = Network.Main;

	[DefaultValue("https://wasabiwallet.io/")]
	[JsonProperty(PropertyName = "MainNetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MainNetBackendUri { get; private set; } = "https://wasabiwallet.io/";

	[DefaultValue("https://wasabiwallet.co/")]
	[JsonProperty(PropertyName = "TestNetClearnetBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string TestNetBackendUri { get; private set; } = "https://wasabiwallet.co/";

	[DefaultValue("http://localhost:37127/")]
	[JsonProperty(PropertyName = "RegTestBackendUri", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string RegTestBackendUri { get; private set; } = "http://localhost:37127/";

	[JsonProperty(PropertyName = "MainNetCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? MainNetCoordinatorUri { get; private set; }

	[JsonProperty(PropertyName = "TestNetCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? TestNetCoordinatorUri { get; private set; }

	[JsonProperty(PropertyName = "RegTestCoordinatorUri", DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string? RegTestCoordinatorUri { get; private set; }

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "UseTor", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool UseTor { get; set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "TerminateTorOnExit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool TerminateTorOnExit { get; set; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "DownloadNewVersion", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool DownloadNewVersion { get; set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "StartLocalBitcoinCoreOnStartup", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StartLocalBitcoinCoreOnStartup { get; set; } = false;

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "StopLocalBitcoinCoreOnShutdown", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool StopLocalBitcoinCoreOnShutdown { get; set; } = true;

	[JsonProperty(PropertyName = "LocalBitcoinCoreDataDir")]
	public string LocalBitcoinCoreDataDir { get; set; } = EnvironmentHelpers.GetDefaultBitcoinCoreDataDirOrEmptyString();

	[JsonProperty(PropertyName = "MainNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
	public EndPoint MainNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultMainNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "TestNetBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
	public EndPoint TestNetBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultTestNetBitcoinP2pPort);

	[JsonProperty(PropertyName = "RegTestBitcoinP2pEndPoint")]
	[JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinP2pPort)]
	public EndPoint RegTestBitcoinP2pEndPoint { get; internal set; } = new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort);

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "JsonRpcServerEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool JsonRpcServerEnabled { get; internal set; }

	[DefaultValue("")]
	[JsonProperty(PropertyName = "JsonRpcUser", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string JsonRpcUser { get; internal set; } = "";

	[DefaultValue("")]
	[JsonProperty(PropertyName = "JsonRpcPassword", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string JsonRpcPassword { get; internal set; } = "";

	[JsonProperty(PropertyName = "JsonRpcServerPrefixes")]
	public string[] JsonRpcServerPrefixes { get; internal set; } = new[]
	{
			"http://127.0.0.1:37128/",
			"http://localhost:37128/"
		};

	[JsonProperty(PropertyName = "DustThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money DustThreshold { get; set; } = DefaultDustThreshold;

	[JsonProperty(PropertyName = "EnableGpu")]
	public bool EnableGpu { get; set; } = true;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonProperty(PropertyName = "CoordinatorIdentifier", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoordinatorIdentifier { get; set; } = "CoinJoinCoordinatorIdentifier";

	public void SetBitcoinP2pEndpoint(EndPoint endPoint)
	{
		if (Network == Network.Main)
		{
			MainNetBitcoinP2pEndPoint = endPoint;
		}
		else if (Network == Network.TestNet)
		{
			TestNetBitcoinP2pEndPoint = endPoint;
		}
		else if (Network == Network.RegTest)
		{
			RegTestBitcoinP2pEndPoint = endPoint;
		}
		else
		{
			throw new NotSupportedNetworkException(Network);
		}
	}

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

	public bool MigrateOldDefaultBackendUris()
	{
		bool hasChanged = false;

		if (MainNetBackendUri == "https://wasabiwallet.io/")
		{
			MainNetBackendUri = "https://api.wasabiwallet.io/";
			hasChanged = true;
		}

		if (TestNetBackendUri == "https://wasabiwallet.co/")
		{
			TestNetBackendUri = "https://api.wasabiwallet.co/";
			hasChanged = true;
		}

		return hasChanged;
	}
}

public class Settings
{
	public Config Config { get; }
	private string[] Args { get; }

	public Settings (Config config, string[] args)
	{
		Config = config;
		Args = args;
	}

	public Network Network => GetEffectiveValue<Network>(Config.Network, x => Network.GetNetwork(x) ?? throw new ArgumentException("Network", $"Unknown network {x}"));
	public string MainNetBackendUri => GetEffectiveString(Config.MainNetBackendUri);
	public string TestNetBackendUri => GetEffectiveString(Config.TestNetBackendUri);
	public string RegTestBackendUri => GetEffectiveString(Config.RegTestBackendUri);
	public string? MainNetCoordinatorUri => GetEffectiveOptionalString(Config.MainNetCoordinatorUri);
	public string? TestNetCoordinatorUri => GetEffectiveOptionalString(Config.TestNetCoordinatorUri);
	public string? RegTestCoordinatorUri => GetEffectiveOptionalString(Config.RegTestCoordinatorUri);
	public bool UseTor => GetEffectiveBool(Config.UseTor);
	public bool TerminateTorOnExit => GetEffectiveBool(Config.TerminateTorOnExit);
	public bool DownloadNewVersion => GetEffectiveBool(Config.DownloadNewVersion);
	public bool StartLocalBitcoinCoreOnStartup => GetEffectiveBool(Config.StartLocalBitcoinCoreOnStartup);
	public bool StopLocalBitcoinCoreOnShutdown => GetEffectiveBool(Config.StopLocalBitcoinCoreOnShutdown);
	public string LocalBitcoinCoreDataDir => GetEffectiveString(Config.LocalBitcoinCoreDataDir);
	public EndPoint MainNetBitcoinP2pEndPoint => GetEffectiveEndPoint(Config.MainNetBitcoinP2pEndPoint);
	public EndPoint TestNetBitcoinP2pEndPoint => GetEffectiveEndPoint(Config.TestNetBitcoinP2pEndPoint);
	public EndPoint RegTestBitcoinP2pEndPoint => GetEffectiveEndPoint(Config.RegTestBitcoinP2pEndPoint);
	public bool JsonRpcServerEnabled => GetEffectiveBool(Config.JsonRpcServerEnabled);
	public string JsonRpcUser => GetEffectiveString(Config.JsonRpcUser);
	public string JsonRpcPassword => GetEffectiveString(Config.JsonRpcPassword);
	public string[] JsonRpcServerPrefixes => GetEffectiveValue(Config.JsonRpcServerPrefixes, x => new [] { x });
	public Money DustThreshold => GetEffectiveValue(Config.DustThreshold, x =>
	{
		if (Money.TryParse(x, out var money))
		{
			return money;
		}
		throw new ArgumentNullException("DustThreshold", "Not a valid money");
	});

	public bool BlockOnlyMode => GetEffectiveBool(false, "blockonly");
	public bool EnableGpu => GetEffectiveBool(Config.EnableGpu);
	public string CoordinatorIdentifier => GetEffectiveString(Config.CoordinatorIdentifier);
	public ServiceConfiguration ServiceConfiguration => new (GetBitcoinP2pEndPoint(), DustThreshold);

	private EndPoint GetEffectiveEndPoint(
		EndPoint valueInConfigFile,
		[CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(
			valueInConfigFile,
			x =>
			{
				if (EndPointParser.TryParse(x, 0, out var endpoint))
				{
					return endpoint;
				}

				throw new ArgumentNullException(key, "Not a valid endpoint");
			},
			key);

	private bool GetEffectiveBool(
		bool valueInConfigFile,
		[CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(
			valueInConfigFile,
			x => x switch
			{
				_ when x.Equals("true", StringComparison.InvariantCultureIgnoreCase) => true,
				_ when x.Equals("false", StringComparison.InvariantCultureIgnoreCase) => false,
				_ => throw new ArgumentNullException(key, "must be 'true' or 'false'")
			},
			key);

	private string GetEffectiveString(
		string valueInConfigFile,
		[CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(valueInConfigFile, x => x, key);

	private string? GetEffectiveOptionalString(
		string? valueInConfigFile,
		[CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(valueInConfigFile, x => x, key);

	private T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, [CallerArgumentExpression("valueInConfigFile")] string key = "")
	{
		key = key.Remove(0, nameof(Config).Length + 1);
		var cliArgKey = ("--" + key + "=");
		var cliArgOrNull = Args.FirstOrDefault(a => a.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase));
		if (cliArgOrNull is { } cliArg)
		{
			return converter(cliArg.Substring(cliArgKey.Length));
		}

		var envKey = "WASABI-" + key.ToUpperInvariant();
		var envVars = Environment.GetEnvironmentVariables();
		if (envVars.Contains(envKey))
		{
			if (envVars[envKey] is string envVar)
			{
				return converter(envVar);
			}
		}

		return valueInConfigFile;
	}

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

	public Uri GetCoordinatorUri()
	{
		var result = Network switch
		{
			{ } n when n == Network.Main => MainNetCoordinatorUri,
			{ } n when n == Network.TestNet => TestNetCoordinatorUri,
			{ } n when n == Network.RegTest => RegTestCoordinatorUri,
			_ => throw new NotSupportedNetworkException(Network)
		};

		return result is null ? GetBackendUri() : new Uri(result);
	}
}
