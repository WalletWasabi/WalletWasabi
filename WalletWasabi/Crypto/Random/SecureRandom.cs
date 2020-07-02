using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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
	}
}
