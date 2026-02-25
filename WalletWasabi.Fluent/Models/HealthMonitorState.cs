namespace WalletWasabi.Fluent.Models;

public enum HealthMonitorState
{
	Loading,
	Ready,
	UpdateAvailable,
	BitcoinRpcIssueDetected,
	BitcoinRpcSynchronizing,
}
