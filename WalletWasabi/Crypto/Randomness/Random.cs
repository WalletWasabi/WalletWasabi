using System.Security.Cryptography;

namespace WalletWasabi.Crypto.Randomness;

public delegate void RandomnessProvider(Span<byte> span);
public delegate string RandomStringGenerator(int length);

public static class RandomnessProviders
{
	private static readonly RandomNumberGenerator SecureRandomNumberGenerator = RandomNumberGenerator.Create();

	public static readonly RandomnessProvider Secure = SecureRandomNumberGenerator.GetBytes;
	public static readonly RandomnessProvider Insecure = Random.Shared.NextBytes;
}

public static class RandomnessProviderExtensions
{
	public static RandomStringGenerator CreateRandomStringGenerator(this RandomnessProvider generator) =>
		length =>
		{
			var result = new char[length];
			for (int i = 0; i < length; i++)
			{
				result[i] = Constants.AlphaNumericCharacters[generator.GetInt(Constants.AlphaNumericCharacters.Length)];
			}
			return new string(result);
		};

	public static int GetInt(this RandomnessProvider generator, int maxExclusive)
	{
		if (maxExclusive <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than 0");
		}

		Span<byte> bytes = stackalloc byte[4];
		generator(bytes);

		var value = BitConverter.ToUInt32(bytes);

		var range = (uint)maxExclusive;
		var max = uint.MaxValue - (uint.MaxValue % range);

		while (value >= max)
		{
			generator(bytes);
			value = BitConverter.ToUInt32(bytes);
		}

		return (int)(value % range);
	}
}
