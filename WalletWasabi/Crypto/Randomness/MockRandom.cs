using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Crypto.Randomness
{
	public class MockRandom : IWasabiRandom
	{
		public List<byte[]> GetBytesResults { get; } = new List<byte[]>();

		public void GetBytes(byte[] output)
		{
			var first = GetBytesResults.First();
			GetBytesResults.RemoveFirst();
			Buffer.BlockCopy(first, 0, output, 0, first.Length);
		}

		public void GetBytes(Span<byte> output)
		{
			var first = GetBytesResults.First();
			GetBytesResults.RemoveFirst();
			first.AsSpan().CopyTo(output);
		}
	}
}
