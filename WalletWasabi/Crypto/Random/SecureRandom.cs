using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.WabiSabi.Crypto
{
	public static class SecureRandom
	{
		public static Scalar GetScalar()
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
	}
}
