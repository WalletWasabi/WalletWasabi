using System;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Crypto
{
	public class OwnershipIdentifier : IBitcoinSerializable
	{
		public const int OwnershipIdLength = 32;
		private byte[] _bytes;

		public OwnershipIdentifier(byte[] bytes)
		{
			if (bytes.Length != OwnershipIdLength)
			{
				throw new ArgumentException($"Ownership identifier must be {OwnershipIdLength} bytes long.");
			}

			_bytes = bytes.ToArray();
		}

		public OwnershipIdentifier()
			: this(new byte[OwnershipIdLength])
		{
		}

		public void ReadWrite(BitcoinStream bitcoinStream)
		{
			bitcoinStream.ReadWrite(ref _bytes);
		}
	}
}
