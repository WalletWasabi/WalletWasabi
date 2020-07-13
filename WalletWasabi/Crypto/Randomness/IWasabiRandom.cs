using NBitcoin;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness
{
	public interface IWasabiRandom : IRandom
	{
		public byte[] GetBytes(int length)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);
			var buffer = new byte[length];
			GetBytes(buffer);
			return buffer;
		}

		public int GetInt(int fromInclusive, int toExclusive);

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

		public string GetString(int length, Characters chars) => GetString(length, chars.Chars);

		public Scalar GetScalar()
		{
			Scalar randomScalar;
			int overflow;
			Span<byte> buffer = stackalloc byte[32];
			do
			{
				GetBytes(buffer);
				randomScalar = new Scalar(buffer, out overflow);
			} while (overflow != 0);
			return randomScalar;
		}
	}
}
