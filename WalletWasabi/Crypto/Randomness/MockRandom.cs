using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.Randomness
{
	public class MockRandom : IWasabiRandom
	{
		public void GetBytes(byte[] output)
		{
			throw new NotImplementedException();
		}

		public void GetBytes(Span<byte> output)
		{
			throw new NotImplementedException();
		}
	}
}
