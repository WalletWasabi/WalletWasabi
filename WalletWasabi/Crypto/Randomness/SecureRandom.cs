using System.Security.Cryptography;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.Randomness;

public class SecureRandom : WasabiRandom
{
	public static readonly SecureRandom Instance = new();

	public override void GetBytes(byte[] buffer)
	{
		RandomNumberGenerator.Fill(buffer);
	}

	public override void GetBytes(Span<byte> buffer)
	{
		RandomNumberGenerator.Fill(buffer);
	}

	public override int GetInt(int fromInclusive, int toExclusive)
	{
		return RandomNumberGenerator.GetInt32(fromInclusive, toExclusive);
	}
}
