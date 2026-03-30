namespace WalletWasabi.Wallets.FilterProcessor;

/// <summary>
/// Thrown when a block filter required for wallet synchronization is not found.
/// This is expected during wallet recovery and requires a restart to resume syncing.
/// </summary>
public class WalletFilterNotFoundException : Exception
{
	public WalletFilterNotFoundException(string message) : base(message)
	{
	}
}
