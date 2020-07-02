using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Crypto
{
	public static class SecureRandom
	{
		public static Scalar GetScalarNonZero()
		{
			using var randomGenerator = RandomNumberGenerator.Create();
			Scalar randomScalar;
			int overflow;
			Span<byte> buffer = stackalloc byte[32];
			do
			{
				randomGenerator.GetBytes(buffer);
				randomScalar = new Scalar(buffer, out overflow);
			} while (overflow != 0 || randomScalar.IsZero);
			return randomScalar;
		}

		public static byte[] GetBytes(int length)
		{
			using var randomGenerator = RandomNumberGenerator.Create();
			var buffer = new byte[length];
			randomGenerator.GetBytes(buffer);
			return buffer;
		}

		public static string GetString(int length, string chars = Constants.AlphaNumericChars)
		{
			Guard.MinimumAndNotNull(nameof(length), length, 1);
			Guard.NotNullOrEmpty(nameof(chars), chars);

			var random = new string(Enumerable
				.Repeat(chars, length)
				.Select(s => s[RandomNumberGenerator.GetInt32(s.Length)])
				.ToArray());
			return random;
		}
	}
}
