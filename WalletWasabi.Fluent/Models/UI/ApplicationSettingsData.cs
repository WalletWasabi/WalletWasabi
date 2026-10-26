using NBitcoin;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models.UI;

/// <summary>
/// DTO for transferring application settings from the backend to the UI.
/// This abstraction allows IServices to be implemented remotely without
/// exposing backend types like PersistentConfig or Config.
/// </summary>
public record ApplicationSettingsData
{
	// Connection
	public required TorMode UseTor { get; init; }
	public required string ExchangeRateProvider { get; init; }
	public required string FeeRateEstimationProvider { get; init; }
	public required string ExternalTransactionBroadcaster { get; init; }

	// Bitcoin
	public required Network Network { get; init; }
	public required string BitcoinRpcUri { get; init; }
	public required string BitcoinRpcCredentialString { get; init; }
	public required decimal DustThreshold { get; init; }

	// Coordinator
	public required string CoordinatorUri { get; init; }
	public required decimal MaxCoinJoinMiningFeeRate { get; init; }
	public required int AbsoluteMinInputCount { get; init; }

	// General
	public required bool TerminateTorOnExit { get; init; }
	public required bool DownloadNewVersion { get; init; }
	public required bool EnableGpu { get; init; }

	// Experimental
	public required string[] ExperimentalFeatures { get; init; }

	// From Config
	public required bool IsOverridden { get; init; }
}
