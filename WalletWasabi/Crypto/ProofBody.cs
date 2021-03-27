using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;

namespace WalletWasabi.Crypto
{
	public class ProofBody : IBitcoinSerializable
	{
		private static readonly byte[] VersionMagic = { 0x53, 0x4c, 0x00, 0x19 };
		private List<OwnershipIdentifier> _ownershipIdentifiers = new();
		private byte _flags;

		public ProofBody()
		{
		}

		public ProofBody(ProofBodyFlags flags, params OwnershipIdentifier[] ownershipIdentifiers)
		{
			Flags = flags;
			_ownershipIdentifiers = ownershipIdentifiers.ToList();
		}

		public IEnumerable<OwnershipIdentifier> OwnershipIdentifiers => _ownershipIdentifiers;

		public ProofBodyFlags Flags
		{
			get => (ProofBodyFlags)_flags;
			set => _flags = (byte)value;
		}

		public void ReadWrite(BitcoinStream bitcoinStream)
		{
			var versionMagic = VersionMagic.ToArray();
			bitcoinStream.ReadWrite(ref versionMagic);

			if (!bitcoinStream.Serializing)
			{
				if (!VersionMagic.SequenceEqual(versionMagic))
				{
					throw new FormatException("Invalid version magic.");
				}
			}

			bitcoinStream.ReadWrite(ref _flags);
			bitcoinStream.ReadWrite(ref _ownershipIdentifiers);
		}

		public uint256 SignatureHash(Script scriptPubKey, byte[] commitmentData) =>
			new(Hashes.SHA256(this.ToBytes().Concat(ProofFooter(scriptPubKey, commitmentData)).ToArray()));

		private static IEnumerable<byte> ProofFooter(Script scriptPubKey, byte[] commitmentData) =>
			scriptPubKey.ToBytes().Concat(commitmentData);
	}
}

