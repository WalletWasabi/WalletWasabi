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
		// We use the TimeLock distribution observed in the bitcoin network
		// in order to reduce the wasabi wallet transactions fingerprinting
		// chances.
		//
		// Network observations:
		// 90.0% uses LockTime = 0
		//  7.5% uses LockTime = current tip
		//  0.65% uses LockTime = next tip (current tip + 1)
		//  1.85% uses up to 5 blocks in the future (we don't do this)
		//  0.65% uses an uniform random from -1 to -99

		// sometimes pick LockTime a bit further back, to help privacy.
		var randomValue = _random.NextDouble();
		return randomValue switch
		{
			var r when r < (0.9) => LockTime.Zero,
			var r when r < (0.9 + 0.075) => tipHeight,
			var r when r < (0.9 + 0.075 + 0.0065) => tipHeight + 1,
			_ => (uint)(tipHeight - _random.Next(1, 100))
		};
	}
}
