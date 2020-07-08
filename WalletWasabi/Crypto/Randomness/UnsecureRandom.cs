using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.Randomness
{
	public class UnsecureRandom : IWasabiRandom
	{
		public UnsecureRandom()
		{
			Random = new Random();
		}

		private Random Random { get; }

		public void GetBytes(byte[] buffer) => Random.NextBytes(buffer);

		public void GetBytes(Span<byte> buffer) => Random.NextBytes(buffer);
	}
}
