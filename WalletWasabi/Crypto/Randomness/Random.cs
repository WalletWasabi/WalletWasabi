using System.Security.Cryptography;

namespace WalletWasabi.Crypto.Randomness;

public delegate byte[] RandomnessProvider(int length);

public static class RandomnessProviders
{
	public static RandomnessProvider Secure =
		length => RandomNumberGenerator.GetBytes(length);

	public static RandomnessProvider Insecure =
		length =>
		{
			var buffer = new byte[length];
			Random.Shared.NextBytes(buffer);
			return buffer;
		};
}

public static class RandomnessProviderExtensions
{
	public static int GetInt(this RandomnessProvider generator, int maxExclusive)
	{
		if (maxExclusive <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than 0");
		}

		var range = (uint)maxExclusive;

		var bytes = generator(4);
		var value = BitConverter.ToUInt32(bytes, 0);

		var max = uint.MaxValue - (uint.MaxValue % range);
		while (value >= max)
		{
			bytes = generator(4);
			value = BitConverter.ToUInt32(bytes, 0);
		}

		return (int)(value % range);
	}
}
