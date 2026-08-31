using System;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Client.Configuration;

public interface IPersistentConfig;

public record PersistentConfig(
	Network Network,
	string CoordinatorUri,
	string UseTor,
	bool TerminateTorOnExit,
	ValueList<string> TorBridges,
	bool DownloadNewVersion,
	string BitcoinRpcCredentialString,
	string BitcoinRpcUri,
	bool JsonRpcServerEnabled,
	string JsonRpcUser,
	string JsonRpcPassword,
	ValueList<string> JsonRpcServerPrefixes,
	Money DustThreshold,
	bool EnableGpu,
	string CoordinatorIdentifier,
	string ExchangeRateProvider,
	string FeeRateEstimationProvider,
	string ExternalTransactionBroadcaster,
	decimal MaxCoinJoinMiningFeeRate,
	int AbsoluteMinInputCount,
	int MaxDaysInMempool,
	ValueList<string> ExperimentalFeatures,
	int ConfigVersion
	) : IPersistentConfig
{
	public string GetConfigFileName() =>
		Network switch
		{
			_ when Network == Network.Main => "Config.json",
			_ when Network == Network.TestNet => "Config.TestNet.json",
			_ when Network == Network.RegTest => "Config.RegTest.json",
			_ when Network == Bitcoin.Instance.Signet => "Config.Signet.json",
			_ => throw new NotSupportedException("Unsupported network")
		};
}

public record PersistentConfig_2_6_0(
	Network Network,
	string IndexerUri,
	string CoordinatorUri,
	string UseTor,
	bool TerminateTorOnExit,
	ValueList<string> TorBridges,
	bool DownloadNewVersion,
	bool UseBitcoinRpc,
	string BitcoinRpcCredentialString,
	string BitcoinRpcUri,
	bool JsonRpcServerEnabled,
	string JsonRpcUser,
	string JsonRpcPassword,
	ValueList<string> JsonRpcServerPrefixes,
	Money DustThreshold,
	bool EnableGpu,
	string CoordinatorIdentifier,
	string ExchangeRateProvider,
	string FeeRateEstimationProvider,
	string ExternalTransactionBroadcaster,
	decimal MaxCoinJoinMiningFeeRate,
	int AbsoluteMinInputCount,
	int MaxDaysInMempool,
	ValueList<string> ExperimentalFeatures,
	int ConfigVersion
) : IPersistentConfig;
