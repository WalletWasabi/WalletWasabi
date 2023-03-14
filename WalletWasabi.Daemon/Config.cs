using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using NBitcoin;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Daemon;

public class Config
{
	public PersistentConfig PersistentConfig { get; }
	private string[] Args { get; }

	public Config (PersistentConfig persistentConfig, string[] args)
	{
		PersistentConfig = persistentConfig;
		Args = args;
	}

	public Network Network => GetEffectiveValue<Network>(PersistentConfig.Network, x => Network.GetNetwork(x) ?? throw new ArgumentException("Network", $"Unknown network {x}"));
	public string MainNetBackendUri => GetEffectiveString(PersistentConfig.MainNetBackendUri);
	public string TestNetBackendUri => GetEffectiveString(PersistentConfig.TestNetBackendUri);
	public string RegTestBackendUri => GetEffectiveString(PersistentConfig.RegTestBackendUri);
	public string? MainNetCoordinatorUri => GetEffectiveOptionalString(PersistentConfig.MainNetCoordinatorUri);
	public string? TestNetCoordinatorUri => GetEffectiveOptionalString(PersistentConfig.TestNetCoordinatorUri);
	public string? RegTestCoordinatorUri => GetEffectiveOptionalString(PersistentConfig.RegTestCoordinatorUri);
	public bool UseTor => GetEffectiveBool(PersistentConfig.UseTor);
	public bool TerminateTorOnExit => GetEffectiveBool(PersistentConfig.TerminateTorOnExit);
	public bool DownloadNewVersion => GetEffectiveBool(PersistentConfig.DownloadNewVersion);
	public bool StartLocalBitcoinCoreOnStartup => GetEffectiveBool(PersistentConfig.StartLocalBitcoinCoreOnStartup);
	public bool StopLocalBitcoinCoreOnShutdown => GetEffectiveBool(PersistentConfig.StopLocalBitcoinCoreOnShutdown);
	public string LocalBitcoinCoreDataDir => GetEffectiveString(PersistentConfig.LocalBitcoinCoreDataDir);
	public EndPoint MainNetBitcoinP2pEndPoint => GetEffectiveEndPoint(PersistentConfig.MainNetBitcoinP2pEndPoint);
	public EndPoint TestNetBitcoinP2pEndPoint => GetEffectiveEndPoint(PersistentConfig.TestNetBitcoinP2pEndPoint);
	public EndPoint RegTestBitcoinP2pEndPoint => GetEffectiveEndPoint(PersistentConfig.RegTestBitcoinP2pEndPoint);
	public bool JsonRpcServerEnabled => GetEffectiveBool(PersistentConfig.JsonRpcServerEnabled);
	public string JsonRpcUser => GetEffectiveString(PersistentConfig.JsonRpcUser);
	public string JsonRpcPassword => GetEffectiveString(PersistentConfig.JsonRpcPassword);
	public string[] JsonRpcServerPrefixes => GetEffectiveValue(PersistentConfig.JsonRpcServerPrefixes, x => new [] { x });
	public Money DustThreshold => GetEffectiveValue(PersistentConfig.DustThreshold, x =>
	{
		if (Money.TryParse(x, out var money))
		{
			return money;
		}
		throw new ArgumentNullException("DustThreshold", "Not a valid money");
	});

	public bool BlockOnlyMode => GetEffectiveBool(false, "blockonly");
	public string LogLevel => GetEffectiveString("", "loglevel");

	public static string DataDir => GetString(
		EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")),
		Environment.GetCommandLineArgs(),
		"datadir");

	public bool EnableGpu => GetEffectiveBool(PersistentConfig.EnableGpu);
	public string CoordinatorIdentifier => GetEffectiveString(PersistentConfig.CoordinatorIdentifier);
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
			Args,
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
			Args,
			key);

	private string GetEffectiveString(
		string valueInConfigFile,
		[CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(valueInConfigFile, x => x, Args, key);

	private string? GetEffectiveOptionalString(
		string? valueInConfigFile,
		[CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(valueInConfigFile, x => x, Args, key);

	private T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, [CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(valueInConfigFile, converter, Args, key);

	private static string GetString(
		string valueInConfigFile, string[] args, [CallerArgumentExpression("valueInConfigFile")] string key = "") =>
		GetEffectiveValue(valueInConfigFile, x => x, args, key);

	private static T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, string[] args, [CallerArgumentExpression("valueInConfigFile")] string key = "")
	{
		key = key.StartsWith(nameof(PersistentConfig) + ".")
			? key.Remove(0, nameof(PersistentConfig).Length + 1)
			: key;

		var cliArgKey = ("--" + key + "=");
		var cliArgOrNull = args.FirstOrDefault(a => a.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase));
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
