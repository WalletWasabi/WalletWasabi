using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Crypto;

public class ProofBody : IBitcoinSerializable, IEquatable<ProofBody>
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
		bitcoinStream.ReadWrite(versionMagic);

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

	private static IEnumerable<byte> ProofFooter(Script scriptPubKey, byte[] commitmentData)
	{
		var scriptPubKeyBytes = scriptPubKey.ToBytes();
		var scriptPubKeyPrefix = new VarInt((ulong)scriptPubKeyBytes.Length).ToBytes();
		var commitmentDataPrefix = new VarInt((ulong)commitmentData.Length).ToBytes();

		return scriptPubKeyPrefix.Concat(scriptPubKeyBytes).Concat(commitmentDataPrefix).Concat(commitmentData);
	}

	public override int GetHashCode()
	{
		int hc = 0;

		foreach (var element in _ownershipIdentifiers)
		{
			hc ^= element.GetHashCode();
			hc = (hc << 7) | (hc >> (32 - 7));
		}

		return HashCode.Combine(_flags.GetHashCode(), hc);
	}

	public static bool operator ==(ProofBody? x, ProofBody? y) => x?.Equals(y) ?? false;

	public static bool operator !=(ProofBody? x, ProofBody? y) => !(x == y);

	public override bool Equals(object? other) => Equals(other as ProofBody);

	public bool Equals(ProofBody? other)
	{
		if (other is null)
		{
			return false;
		}

		return Flags == other.Flags && OwnershipIdentifiers.SequenceEqual(other.OwnershipIdentifiers);
	}
}
