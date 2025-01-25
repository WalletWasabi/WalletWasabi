namespace WalletWasabi.Backend.Models;

/// <summary>
/// Satoshi per byte.
/// </summary>
public class FeeEstimationPair
{
	public long Economical { get; set; }

	public long Conservative { get; set; }
}
