using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

/// <seealso href="https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/"/>>
public class InsecureRandom : WasabiRandom
{
	public static readonly InsecureRandom Instance = new();

	public InsecureRandom()
	{
		Random = Random.Shared;
	}

	public InsecureRandom(int seed)
	{
		Random = new Random(seed);
	}

	private Random Random { get; }

	public override void GetBytes(byte[] buffer) => Random.NextBytes(buffer);

	public override void GetBytes(Span<byte> buffer) => Random.NextBytes(buffer);

	public override int GetInt(int fromInclusive, int toExclusive) => Random.Next(fromInclusive, toExclusive);

	public long GetInt64(long fromInclusive, long toExclusive) => Random.NextInt64(fromInclusive, toExclusive);
}
