using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tests.Helpers;

public static class RandomExtensions
{
	public static RandomnessProvider CreateSeeded(int seed)
	{
		var random = new Random(seed);
		return random.NextBytes;
	}

	public static long GetInt64(this RandomnessProvider generator, long fromInclusive, long toExclusive)
	{
		if (fromInclusive >= toExclusive)
		{
			throw new ArgumentOutOfRangeException(nameof(toExclusive), "toExclusive must be greater than fromInclusive");
		}

		var range = (ulong)(toExclusive - fromInclusive);

		Span<byte> bytes = stackalloc byte[8];
		generator(bytes);
		var value = BitConverter.ToUInt64(bytes);

		var max = ulong.MaxValue - (ulong.MaxValue % range);
		while (value >= max)
		{
			generator(bytes);
			value = BitConverter.ToUInt64(bytes);
		}

		return (long)(value % range) + fromInclusive;
	}
}
