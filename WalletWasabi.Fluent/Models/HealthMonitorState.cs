namespace WalletWasabi.Fluent.Models;

public enum HealthMonitorState
{
	Loading,
	Ready,
	IndexerNotCompatible,
	UpdateAvailable,
	ConnectionIssueDetected,
	BitcoinCoreIssueDetected,
	BitcoinCoreSynchronizingOrConnecting,
}
