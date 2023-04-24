using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

public class DeterministicRandom : WasabiRandom
{
	public DeterministicRandom(int seed)
	{
		Random = new Random(seed);
	}

	private Random Random { get; }

	public override void GetBytes(byte[] buffer) => Random.NextBytes(buffer);

	public override void GetBytes(Span<byte> buffer) => Random.NextBytes(buffer);

	public override int GetInt(int fromInclusive, int toExclusive) => Random.Next(fromInclusive, toExclusive);

	public long GetLongInt(long fromInclusive, long toExclusive) => Random.NextInt64(fromInclusive, toExclusive);
}
