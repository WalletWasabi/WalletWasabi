using NBitcoin;

namespace WalletWasabi.Wallets;

/// <summary>
/// Produces random <see cref="LockTime"/> value for a new transaction based on observed lock-time values distribution in Bitcoin mainnet network.
/// </summary>
/// <remarks>Helps avoid fingerprinting of Wasabi Wallet transactions.</remarks>
public class LockTimeSelector
{
	static LockTimeSelector()
	{
		Instance = new LockTimeSelector(Random.Shared);
	}

	public LockTimeSelector(Random random)
	{
		_random = random;
	}

	public static LockTimeSelector Instance { get; }

	private readonly Random _random;

	public LockTime GetLockTimeBasedOnDistribution(uint tipHeight)
	{
		// Use a distribution based on observed lock times to reduce fingerprinting:
		// 90.0% uses LockTime = 0, 8.15% uses the current tip, and 1.85% uses a recent height.
		// Fold the observed next-tip bucket into the current tip: Bitcoin Core only relays
		// transactions whose lock time is strictly below the next block height.

		// sometimes pick LockTime a bit further back, to help privacy.
		var randomValue = _random.NextDouble();
		return randomValue switch
		{
			var r when r < (0.9) => LockTime.Zero,
			var r when r < (0.9 + 0.075 + 0.0065) => tipHeight,
			_ => (uint)Math.Max(0, (long)tipHeight - _random.Next(1, 100))
		};
	}
}
