namespace WalletWasabi.Fluent.Models;

public enum HealthMonitorState
{
	Loading,
	Ready,
	BackendNotCompatible,
	UpdateAvailable,
	ConnectionIssueDetected,
	BitcoinCoreIssueDetected,
	BitcoinCoreSynchronizingOrConnecting,
}
