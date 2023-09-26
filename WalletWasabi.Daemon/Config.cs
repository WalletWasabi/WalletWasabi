using System;
using System.IO;
using System.Net;
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

	public Config(PersistentConfig persistentConfig, string[] args)
	{
		PersistentConfig = persistentConfig;
		Args = args;
	}

	public Network Network => GetEffectiveValue<Network>(
		PersistentConfig.Network,
		x => Network.GetNetwork(x) ?? throw new ArgumentException("Network", $"Unknown network {x}"),
		key: "Network");

	public string MainNetBackendUri => GetEffectiveString(PersistentConfig.MainNetBackendUri, key: "MainNetBackendUri");
	public string TestNetBackendUri => GetEffectiveString(PersistentConfig.TestNetBackendUri, key: "TestNetBackendUri");
	public string RegTestBackendUri => GetEffectiveString(PersistentConfig.RegTestBackendUri, key: "RegTestBackendUri");
	public string? MainNetCoordinatorUri => GetEffectiveOptionalString(PersistentConfig.MainNetCoordinatorUri, key: "MainNetCoordinatorUri");
	public string? TestNetCoordinatorUri => GetEffectiveOptionalString(PersistentConfig.TestNetCoordinatorUri, key: "TestNetCoordinatorUri");
	public string? RegTestCoordinatorUri => GetEffectiveOptionalString(PersistentConfig.RegTestCoordinatorUri, key: "RegTestCoordinatorUri");
	public bool UseTor => GetEffectiveBool(PersistentConfig.UseTor, key: "UseTor");
	public bool TerminateTorOnExit => GetEffectiveBool(PersistentConfig.TerminateTorOnExit, key: "TerminateTorOnExit");
	public bool DownloadNewVersion => GetEffectiveBool(PersistentConfig.DownloadNewVersion, key: "DownloadNewVersion");
	public bool StartLocalBitcoinCoreOnStartup => GetEffectiveBool(PersistentConfig.StartLocalBitcoinCoreOnStartup, key: "StartLocalBitcoinCoreOnStartup");
	public bool StopLocalBitcoinCoreOnShutdown => GetEffectiveBool(PersistentConfig.StopLocalBitcoinCoreOnShutdown, key: "StopLocalBitcoinCoreOnShutdown");
	public string LocalBitcoinCoreDataDir => GetEffectiveString(PersistentConfig.LocalBitcoinCoreDataDir, key: "LocalBitcoinCoreDataDir");
	public EndPoint MainNetBitcoinP2pEndPoint => GetEffectiveEndPoint(PersistentConfig.MainNetBitcoinP2pEndPoint, key: "MainNetBitcoinP2pEndPoint");
	public EndPoint TestNetBitcoinP2pEndPoint => GetEffectiveEndPoint(PersistentConfig.TestNetBitcoinP2pEndPoint, key: "TestNetBitcoinP2pEndPoint");
	public EndPoint RegTestBitcoinP2pEndPoint => GetEffectiveEndPoint(PersistentConfig.RegTestBitcoinP2pEndPoint, key: "RegTestBitcoinP2pEndPoint");
	public bool JsonRpcServerEnabled => GetEffectiveBool(PersistentConfig.JsonRpcServerEnabled, key: "JsonRpcServerEnabled");
	public string JsonRpcUser => GetEffectiveString(PersistentConfig.JsonRpcUser, key: "JsonRpcUser");
	public string JsonRpcPassword => GetEffectiveString(PersistentConfig.JsonRpcPassword, key: "JsonRpcPassword");
	public string[] JsonRpcServerPrefixes => GetEffectiveValue(PersistentConfig.JsonRpcServerPrefixes, x => new[] { x }, key: "JsonRpcServerPrefixes");

	public Money DustThreshold => GetEffectiveValue(PersistentConfig.DustThreshold, x =>
		{
			if (Money.TryParse(x, out var money))
			{
				return money;
			}
			throw new ArgumentNullException("DustThreshold", "Not a valid money");
		},
		key: "DustThreshold");

	public bool BlockOnlyMode => GetEffectiveBool(false, "BlockOnly");
	public string LogLevel => GetEffectiveString("", "LogLevel");

	public static string DataDir => GetString(
		EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")),
		Environment.GetCommandLineArgs(),
		"datadir");

	public bool EnableGpu => GetEffectiveBool(PersistentConfig.EnableGpu, key: "EnableGpu");
	public string CoordinatorIdentifier => GetEffectiveString(PersistentConfig.CoordinatorIdentifier, key: "CoordinatorIdentifier");
	public ServiceConfiguration ServiceConfiguration => new(GetBitcoinP2pEndPoint(), DustThreshold);

	private EndPoint GetEffectiveEndPoint(EndPoint valueInConfigFile, string key) =>
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

	private bool GetEffectiveBool(bool valueInConfigFile, string key) =>
		GetEffectiveValue(
			valueInConfigFile,
			x =>
				bool.TryParse(x, out var value)
				? value
				: throw new ArgumentException("must be 'true' or 'false'.", key),
			Args,
			key);

	private string GetEffectiveString(string valueInConfigFile, string key) =>
		GetEffectiveValue(valueInConfigFile, x => x, Args, key);

	private string? GetEffectiveOptionalString(string? valueInConfigFile, string key) =>
		GetEffectiveValue(valueInConfigFile, x => x, Args, key);

	private T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, string key) =>
		GetEffectiveValue(valueInConfigFile, converter, Args, key);

	private static string GetString(string valueInConfigFile, string[] args, string key) =>
		GetEffectiveValue(valueInConfigFile, x => x, args, key);

	private static T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, string[] args, string key)
	{
		if (ArgumentHelpers.TryGetValue(key, args, converter, out var cliArg))
		{
			return cliArg;
		}

		var envKey = "WASABI-" + key.ToUpperInvariant();
		var environmentVariables = Environment.GetEnvironmentVariables();
		if (environmentVariables.Contains(envKey))
		{
			if (environmentVariables[envKey] is string envVar)
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
