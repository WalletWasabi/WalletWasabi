namespace WalletWasabi.Fluent.Models;

public enum HealthMonitorState
{
	Loading,
	Ready,
	IncompatibleIndexer,
	UpdateAvailable,
	IndexerConnectionIssueDetected,
	BitcoinRpcIssueDetected,
	BitcoinRpcSynchronizing,
}
