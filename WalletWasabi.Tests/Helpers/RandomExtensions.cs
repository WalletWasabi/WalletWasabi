using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tests.Helpers;

public static class RandomExtensions
{
	public static RandomnessProvider CreateSeeded(int seed)
	{
		var random = new Random(seed);
		return length =>
		{
			var buffer = new byte[length];
			random.NextBytes(buffer);
			return buffer;
		};
	}

	public static long GetInt64(this RandomnessProvider generator, long fromInclusive, long toExclusive)
	{
		if (fromInclusive >= toExclusive)
		{
			throw new ArgumentOutOfRangeException(nameof(toExclusive), "toExclusive must be greater than fromInclusive");
		}

		var range = (ulong)(toExclusive - fromInclusive);

		var bytes = generator(8);
		var value = BitConverter.ToUInt64(bytes, 0);

		var max = ulong.MaxValue - (ulong.MaxValue % range);
		while (value >= max)
		{
			bytes = generator(8);
			value = BitConverter.ToUInt64(bytes, 0);
		}

		return (long)(value % range) + fromInclusive;
	}
}
