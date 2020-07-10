using NBitcoin;
using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
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
