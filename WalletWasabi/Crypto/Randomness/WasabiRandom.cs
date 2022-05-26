using NBitcoin;
using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness;

public abstract class WasabiRandom : IRandom
{
	public abstract void GetBytes(byte[] output);

	public abstract void GetBytes(Span<byte> output);

	public virtual byte[] GetBytes(int length)
	{
		Guard.MinimumAndNotNull(nameof(length), length, 1);
		var buffer = new byte[length];
		GetBytes(buffer);
		return buffer;
	}

	public abstract int GetInt(int fromInclusive, int toExclusive);

	public string GetString(int length, string chars)
	{
		Guard.MinimumAndNotNull(nameof(length), length, 1);
		Guard.NotNullOrEmpty(nameof(chars), chars);

		var random = new string(Enumerable
			.Repeat(chars, length)
			.Select(s => s[GetInt(0, s.Length)])
			.ToArray());
		return random;
	}

	public virtual Scalar GetScalar()
	{
		Scalar randomScalar;
		int overflow;
		Span<byte> buffer = stackalloc byte[32];
		do
		{
			GetBytes(buffer);
			randomScalar = new Scalar(buffer, out overflow);
		}
		while (overflow != 0 || randomScalar.IsZero);
		return randomScalar;
	}
}
