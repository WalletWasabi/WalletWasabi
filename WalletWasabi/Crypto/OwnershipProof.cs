using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;

namespace WalletWasabi.Crypto;

public record OwnershipProof : IBitcoinSerializable
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
		Generate(key, new[] { ownershipIdentifier }, commitmentData, userConfirmation, scriptPubKeyType);

	public static OwnershipProof Generate(Key key, IEnumerable<OwnershipIdentifier> ownershipIdentifiers, byte[] commitmentData, bool userConfirmation, ScriptPubKeyType scriptPubKeyType) =>
		scriptPubKeyType switch
		{
			ScriptPubKeyType.Segwit or ScriptPubKeyType.TaprootBIP86 => GenerateOwnershipProof(key, commitmentData, new ProofBody(userConfirmation ? ProofBodyFlags.UserConfirmation : 0, ownershipIdentifiers.ToArray()), scriptPubKeyType),
			_ => throw new NotImplementedException("Only P2WPKH and P2TR scripts are supported."),
		};

	private static OwnershipProof GenerateOwnershipProof(Key key, byte[] commitmentData, ProofBody proofBody, ScriptPubKeyType scriptPubKeyType) =>
		new(
			proofBody,
			Bip322Signature.Generate(key, proofBody.SignatureHash(key.PubKey.GetScriptPubKey(scriptPubKeyType), commitmentData), scriptPubKeyType));

	public bool VerifyOwnership(Script scriptPubKey, byte[] commitmentData, bool requireUserConfirmation) =>
		scriptPubKey.TryGetScriptType() switch
		{
			ScriptType.P2WPKH or ScriptType.Taproot => VerifyOwnershipProof(scriptPubKey, commitmentData, requireUserConfirmation),
			_ => throw new NotImplementedException("Only P2WPKH and P2TR scripts are supported."),
		};

	private bool VerifyOwnershipProof(Script scriptPubKey, byte[] commitmentData, bool requireUserConfirmation)
	{
		if (requireUserConfirmation && !_proofBody.Flags.HasFlag(ProofBodyFlags.UserConfirmation))
		{
			return false;
		}

		var hash = _proofBody.SignatureHash(scriptPubKey, commitmentData);

		return _proofSignature.Verify(hash, scriptPubKey);
	}

	public static OwnershipProof GenerateCoinJoinInputProof(Key key, OwnershipIdentifier ownershipIdentifier, CoinJoinInputCommitmentData coinJoinInputsCommitmentData, ScriptPubKeyType scriptPubKeyType) =>
		Generate(key, new[] { ownershipIdentifier }, coinJoinInputsCommitmentData.ToBytes(), true, scriptPubKeyType);

	public static bool VerifyCoinJoinInputProof(OwnershipProof ownershipProofBytes, Script scriptPubKey, CoinJoinInputCommitmentData coinJoinInputsCommitmentData) =>
		ownershipProofBytes.VerifyOwnership(scriptPubKey, coinJoinInputsCommitmentData.ToBytes(), true);

	public static OwnershipProof FromBytes(byte[] ownershipProofBytes) =>
		NBitcoinExtensions.FromBytes<OwnershipProof>(ownershipProofBytes);
}
