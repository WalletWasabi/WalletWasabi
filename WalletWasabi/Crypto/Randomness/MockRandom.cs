using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Crypto.Randomness
{
	public class MockRandom : WasabiRandom
	{
		public List<byte[]> GetBytesResults { get; } = new List<byte[]>();

		public override void GetBytes(byte[] output)
		{
			var first = GetBytesResults.First();
			GetBytesResults.RemoveFirst();
			Buffer.BlockCopy(first, 0, output, 0, first.Length);
		}

		public override void GetBytes(Span<byte> output)
		{
			var first = GetBytesResults.First();
			GetBytesResults.RemoveFirst();
			first.AsSpan().CopyTo(output);
		}

		public override int GetInt(int fromInclusive, int toExclusive)
		{
			throw new NotImplementedException();
		}
	}
}
