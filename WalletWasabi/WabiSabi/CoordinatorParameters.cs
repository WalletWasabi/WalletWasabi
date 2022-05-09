using System.IO;
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
		RuntimeCoordinatorConfig.LoadOrCreateDefaultFile();
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

	/// <summary>
	/// How often should UTXOs be serialized and released from prison.
	/// </summary>
	public TimeSpan UtxoWardenPeriod { get; init; } = TimeSpan.FromSeconds(7);

	/// <summary>
	/// How often should rounds be stepped.
	/// </summary>
	public TimeSpan RoundProgressSteppingPeriod { get; init; } = TimeSpan.FromSeconds(1);
}
