using System.IO;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend;

namespace WalletWasabi.WabiSabi;

public class CoordinatorParameters
{
	public CoordinatorParameters(string dataDir)
	{
		ApplicationDataDir = dataDir;
		IoHelpers.EnsureDirectoryExists(CoordinatorDataDir);

		var runtimeConfigurationFilePath = Path.Combine(ApplicationDataDir, "WabiSabiConfig.json");
		RuntimeCoordinatorConfig = new(runtimeConfigurationFilePath);
		RuntimeCoordinatorConfig.LoadFile(createIfMissing: true);
	}

	/// <summary>
	/// The main data directory of the application.
	/// </summary>
	public string ApplicationDataDir { get; }

	/// <summary>
	/// The main data directory of the coordinator.
	/// </summary>
	public string CoordinatorDataDir => Path.Combine(ApplicationDataDir, "WabiSabi");

	/// <summary>
	/// Banned UTXOs are serialized here.
	/// </summary>
	public string PrisonFilePath => Path.Combine(CoordinatorDataDir, "Prison.txt");

	/// <summary>
	/// Whitelisted UTXOs are serialized here.
	/// </summary>
	public string WhitelistFilePath => Path.Combine(CoordinatorDataDir, "Whitelist.txt");

	/// <summary>
	/// File that is storing the hashes of WabiSabi CoinJoins.
	/// </summary>
	public string CoinJoinIdStoreFilePath => Path.Combine(CoordinatorDataDir, "CoinJoinIdStore.txt");

	/// <summary>
	/// File that is storing the fee rate statistics.
	/// </summary>
	public string CoinJoinFeeRateStatStoreFilePath => Path.Combine(CoordinatorDataDir, "CoinJoinFeeRateStatStore.txt");

	public string CoinJoinScriptStoreFilePath => Path.Combine(CoordinatorDataDir, "CoinJoinScriptStore.txt");

	/// <summary>
	/// Runtime adjustable configuration of the coordinator.
	/// </summary>
	public WabiSabiConfig RuntimeCoordinatorConfig { get; }

	/// <summary>
	/// Configuration of coinjoins can be modified runtime.
	/// Set how often changes in the configuration file should be monitored.
	/// </summary>
	public TimeSpan ConfigChangeMonitoringPeriod { get; init; } = TimeSpan.FromSeconds(7);
}
