namespace WalletWasabi.Crypto.Randomness;

public class InsecureRandom : WasabiRandom
{
	public InsecureRandom()
	{
		Random = new Random();
	}

	private Random Random { get; }

	public override void GetBytes(byte[] buffer) => Random.NextBytes(buffer);

	public override void GetBytes(Span<byte> buffer) => Random.NextBytes(buffer);

	public override int GetInt(int fromInclusive, int toExclusive) => Random.Next(fromInclusive, toExclusive);
}
