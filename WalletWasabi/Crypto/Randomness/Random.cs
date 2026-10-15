using System.Security.Cryptography;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

public delegate void RandomnessProvider(Span<byte> span);
public delegate string RandomStringGenerator(int length);

public static class RandomnessProviders
{
	public static readonly RandomnessProvider Secure = RandomNumberGenerator.Fill;
	public static readonly RandomnessProvider Insecure = Random.Shared.NextBytes;

	public static RandomnessProvider CreateSeeded(int seed)
	{
		var random = new Random(seed);
		return random.NextBytes;
	}
}

public static class RandomnessProviderExtensions
{
	extension(RandomnessProvider generator)
	{
		public RandomStringGenerator CreateRandomStringGenerator() =>
			length =>
			{
				var result = new char[length];
				for (var i = 0; i < length; i++)
				{
					result[i] = Constants.AlphaNumericCharacters[generator.GetInt(Constants.AlphaNumericCharacters.Length)];
				}
				return new string(result);
			};

		public int GetInt(int maxExclusive)
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

		public int GetInt(int fromInclusive, int toExclusive)
		{
			if (fromInclusive >= toExclusive)
			{
				throw new ArgumentOutOfRangeException(nameof(toExclusive), "toExclusive must be greater than fromInclusive");
			}

			return fromInclusive + generator.GetInt(toExclusive - fromInclusive);
		}

		public byte[] GetBytes(int length)
		{
			var buffer = new byte[length];
			generator(buffer);
			return buffer;
		}

		public WasabiRandom ToWasabiRandom() =>
			new WasabiRandomAdapter(generator);
	}
}

internal sealed class WasabiRandomAdapter(RandomnessProvider provider) : WasabiRandom
{
	public override void GetBytes(byte[] buffer) => provider(buffer);

	public override void GetBytes(Span<byte> buffer) => provider(buffer);
}
