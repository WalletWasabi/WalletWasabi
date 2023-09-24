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
	public Config(PersistentConfig persistentConfig, string[] args)
	{
		PersistentConfig = persistentConfig;
		Args = args;
		Network = GetEffectiveValue(
			PersistentConfig.Network,
			x => Network.GetNetwork(x) ?? throw new ArgumentException("Network", $"Unknown network {x}"),
			key: "Network");
		MainNetBackendUri = GetEffectiveString(PersistentConfig.MainNetBackendUri, key: "MainNetBackendUri");
		TestNetBackendUri = GetEffectiveString(PersistentConfig.TestNetBackendUri, key: "TestNetBackendUri");
		RegTestBackendUri = GetEffectiveString(PersistentConfig.RegTestBackendUri, key: "RegTestBackendUri");
		MainNetCoordinatorUri = GetEffectiveOptionalString(PersistentConfig.MainNetCoordinatorUri, key: "MainNetCoordinatorUri");
		TestNetCoordinatorUri = GetEffectiveOptionalString(PersistentConfig.TestNetCoordinatorUri, key: "TestNetCoordinatorUri");
		RegTestCoordinatorUri = GetEffectiveOptionalString(PersistentConfig.RegTestCoordinatorUri, key: "RegTestCoordinatorUri");
		UseTor = GetEffectiveBool(PersistentConfig.UseTor, key: "UseTor");
		TerminateTorOnExit = GetEffectiveBool(PersistentConfig.TerminateTorOnExit, key: "TerminateTorOnExit");
		DownloadNewVersion = GetEffectiveBool(PersistentConfig.DownloadNewVersion, key: "DownloadNewVersion");
		StartLocalBitcoinCoreOnStartup = GetEffectiveBool(PersistentConfig.StartLocalBitcoinCoreOnStartup, key: "StartLocalBitcoinCoreOnStartup");
		StopLocalBitcoinCoreOnShutdown = GetEffectiveBool(PersistentConfig.StopLocalBitcoinCoreOnShutdown, key: "StopLocalBitcoinCoreOnShutdown");
		LocalBitcoinCoreDataDir = GetEffectiveString(PersistentConfig.LocalBitcoinCoreDataDir, key: "LocalBitcoinCoreDataDir");
		MainNetBitcoinP2pEndPoint = GetEffectiveEndPoint(PersistentConfig.MainNetBitcoinP2pEndPoint, key: "MainNetBitcoinP2pEndPoint");
		TestNetBitcoinP2pEndPoint = GetEffectiveEndPoint(PersistentConfig.TestNetBitcoinP2pEndPoint, key: "TestNetBitcoinP2pEndPoint");
		RegTestBitcoinP2pEndPoint = GetEffectiveEndPoint(PersistentConfig.RegTestBitcoinP2pEndPoint, key: "RegTestBitcoinP2pEndPoint");
		JsonRpcServerEnabled = GetEffectiveBool(PersistentConfig.JsonRpcServerEnabled, key: "JsonRpcServerEnabled");
		JsonRpcUser = GetEffectiveString(PersistentConfig.JsonRpcUser, key: "JsonRpcUser");
		JsonRpcPassword = GetEffectiveString(PersistentConfig.JsonRpcPassword, key: "JsonRpcPassword");
		JsonRpcServerPrefixes = GetEffectiveValue(PersistentConfig.JsonRpcServerPrefixes, x => new[] { x }, key: "JsonRpcServerPrefixes");
		DustThreshold = GetEffectiveValue(
			PersistentConfig.DustThreshold,
			x =>
			{
				if (Money.TryParse(x, out var money))
				{
					return money;
				}

				throw new ArgumentNullException("DustThreshold", "Not a valid money");
			},
			key: "DustThreshold");
		BlockOnlyMode = GetEffectiveBool(false, "BlockOnly");
		LogLevel = GetEffectiveString("", "LogLevel");
		EnableGpu = GetEffectiveBool(PersistentConfig.EnableGpu, key: "EnableGpu");
		CoordinatorIdentifier = GetEffectiveString(PersistentConfig.CoordinatorIdentifier, key: "CoordinatorIdentifier");
		ServiceConfiguration = new ServiceConfiguration(GetBitcoinP2pEndPoint(), DustThreshold);
	}

	public PersistentConfig PersistentConfig { get; }
	public string[] Args { get; }
	public Network Network { get; }
	public string MainNetBackendUri { get; }
	public string TestNetBackendUri { get; }
	public string RegTestBackendUri { get; }
	public string? MainNetCoordinatorUri { get; }
	public string? TestNetCoordinatorUri { get; }
	public string? RegTestCoordinatorUri { get; }
	public bool UseTor { get; }
	public bool TerminateTorOnExit { get; }
	public bool DownloadNewVersion { get; }
	public bool StartLocalBitcoinCoreOnStartup { get; }
	public bool StopLocalBitcoinCoreOnShutdown { get; }
	public string LocalBitcoinCoreDataDir { get; }
	public EndPoint MainNetBitcoinP2pEndPoint { get; }
	public EndPoint TestNetBitcoinP2pEndPoint { get; }
	public EndPoint RegTestBitcoinP2pEndPoint { get; }
	public bool JsonRpcServerEnabled { get; }
	public string JsonRpcUser { get; }
	public string JsonRpcPassword { get; }
	public string[] JsonRpcServerPrefixes { get; }
	public Money DustThreshold { get; }
	public bool BlockOnlyMode { get; }
	public string LogLevel { get; }

	public bool EnableGpu { get; }
	public string CoordinatorIdentifier { get; }
	public ServiceConfiguration ServiceConfiguration { get; }

	public static string DataDir => GetString(
		EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")),
		Environment.GetCommandLineArgs(),
		"datadir");

	public bool IsOverridden { get; set; }

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

	private static string GetString(string valueInConfigFile, string[] args, string key)
	{
		return GetEffectiveValue(valueInConfigFile, x => x, args, key);
	}

	private static T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, string[] args, string key)
	{
		if (ArgumentHelpers.TryGetValue(key, args, converter, out var cliArg))
		{
			return cliArg;
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

	private EndPoint GetEffectiveEndPoint(EndPoint valueInConfigFile, string key)
	{
		return GetEffectiveValue(
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
	}

	private bool GetEffectiveBool(bool valueInConfigFile, string key)
	{
		return GetEffectiveValue(
			valueInConfigFile,
			x =>
				bool.TryParse(x, out var value)
					? value
					: throw new ArgumentException("must be 'true' or 'false'.", key),
			Args,
			key);
	}

	private string GetEffectiveString(string valueInConfigFile, string key)
	{
		return GetEffectiveValue(valueInConfigFile, x => x, Args, key);
	}

	private string? GetEffectiveOptionalString(string? valueInConfigFile, string key)
	{
		return GetEffectiveValue(valueInConfigFile, x => x, Args, key);
	}

	private T GetEffectiveValue<T>(T valueInConfigFile, Func<string, T> converter, string key)
	{
		var effectiveValue = GetEffectiveValue(valueInConfigFile, converter, Args, key);

		if (!Equals(effectiveValue, valueInConfigFile))
		{
			IsOverridden = true;
		}

		return effectiveValue;
	}
}
