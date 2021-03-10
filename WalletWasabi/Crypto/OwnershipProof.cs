using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;

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

	public class CoinJoinInputCommitmentData
	{
		private byte[] _coordinatorIdentifier;
		private byte[] _roundIdentifier;

		public CoinJoinInputCommitmentData(string coordinatorIdentifier, uint256 roundIdentifier)
			: this(Encoding.ASCII.GetBytes(coordinatorIdentifier), roundIdentifier.ToBytes())
		{
		}

		public CoinJoinInputCommitmentData(byte[] coordinatorIdentifier, byte[] roundIdentifier)
		{
			_coordinatorIdentifier = coordinatorIdentifier;
			_roundIdentifier = roundIdentifier;
		}

		public byte[] ToBytes() =>
			BitConverter.GetBytes(_coordinatorIdentifier.Length)
				.Concat(_coordinatorIdentifier)
				.Concat(_roundIdentifier)
				.ToArray();
	}

	[Flags]
	public enum ProofBodyFlags : byte
	{
		UserConfirmation = 1
	}

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

	public class OwnershipProof : IBitcoinSerializable
	{
		private ProofBody _proofBody;
		private Bip322Signature _proofSignature;

		public OwnershipProof()
			: this(new ProofBody(), new Bip322Signature())
		{
		}

		public OwnershipProof(ProofBody proofBody, Bip322Signature proofSignature)
		{
			_proofBody = proofBody;
			_proofSignature = proofSignature;
		}

		public ProofBody ProofBody => _proofBody;

		public Bip322Signature ProofSignature => _proofSignature;

		public void ReadWrite(BitcoinStream bitcoinStream)
		{
			bitcoinStream.ReadWrite(ref _proofBody);
			bitcoinStream.ReadWrite(ref _proofSignature);
		}

		public static OwnershipProof Generate(Key key, OwnershipIdentifier ownershipIdentifier, byte[] commitmentData, bool userConfirmation, ScriptPubKeyType scriptPubKeyType) =>
			scriptPubKeyType switch
			{
				ScriptPubKeyType.Segwit => GenerateOwnershipProofSegwit(key, commitmentData, new ProofBody(userConfirmation ? ProofBodyFlags.UserConfirmation : 0, ownershipIdentifier)),
				_ => throw new NotImplementedException()
			};

		private static OwnershipProof GenerateOwnershipProofSegwit(Key key, byte[] commitmentData, ProofBody proofBody) =>
			new(
				proofBody,
				Bip322Signature.Generate(key, proofBody.SignatureHash(key.PubKey.WitHash.ScriptPubKey, commitmentData), ScriptPubKeyType.Segwit));

		public bool VerifyOwnership(Script scriptPubKey, byte[] commitmentData, bool requireUserConfirmation) =>
			scriptPubKey.IsScriptType(ScriptType.P2WPKH) switch
			{
				true => VerifyOwnershipProofSegwit(scriptPubKey, commitmentData, requireUserConfirmation),
				false => throw new NotImplementedException()
			};

		private bool VerifyOwnershipProofSegwit(Script scriptPubKey, byte[] commitmentData, bool requireUserConfirmation)
		{
			if (requireUserConfirmation && !_proofBody.Flags.HasFlag(ProofBodyFlags.UserConfirmation))
			{
				return false;
			}

			var hash = _proofBody.SignatureHash(scriptPubKey, commitmentData);

			return _proofSignature.Verify(hash, scriptPubKey);
		}

		public static OwnershipProof GenerateCoinJoinInputProof(Key key, CoinJoinInputCommitmentData coinJoinInputsCommitmentData) =>
			Generate(key, new OwnershipIdentifier(), coinJoinInputsCommitmentData.ToBytes(), true, ScriptPubKeyType.Segwit);

		public static bool VerifyCoinJoinInputProof(byte[] ownershipProofBytes, Script scriptPubKey, CoinJoinInputCommitmentData coinJoinInputsCommitmentData) =>
			FromBytes(ownershipProofBytes).VerifyOwnership(scriptPubKey, coinJoinInputsCommitmentData.ToBytes(), true);

		public static OwnershipProof FromBytes(byte[] ownershipProofBytes) =>
			NBitcoinExtensions.FromBytes<OwnershipProof>(ownershipProofBytes);
	}
}
