using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

/// <seealso href="https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/"/>>
public class InsecureRandom : WasabiRandom
{
	public static readonly InsecureRandom Instance = new();

	public InsecureRandom()
	{
		_random = Random.Shared;
	}

	public InsecureRandom(int seed)
	{
		_random = new Random(seed);
	}

	private readonly Random _random;

	public override void GetBytes(byte[] buffer) => _random.NextBytes(buffer);

	public override void GetBytes(Span<byte> buffer) => _random.NextBytes(buffer);

	public override int GetInt(int fromInclusive, int toExclusive) => _random.Next(fromInclusive, toExclusive);

	public long GetInt64(long fromInclusive, long toExclusive) => _random.NextInt64(fromInclusive, toExclusive);
}
