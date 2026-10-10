using NativeOwnershipProof = WabiSabi.Native.OwnershipProof;
using NativeScriptPubKeyType = WabiSabi.Native.OwnershipScriptPubKeyType;

namespace WalletWasabi.WabiSabi.Crypto;

/// <summary>
/// Native implementation of ownership proof generation and verification.
/// Exposes the same API as <see cref="OwnershipProof"/> for drop-in replacement.
/// </summary>
public static class OwnershipProofProvider
{
	public static OwnershipProof GenerateCoinJoinInputProof(
		Key key,
		OwnershipIdentifier ownershipIdentifier,
		CoinJoinInputCommitmentData coinJoinInputsCommitmentData,
		ScriptPubKeyType scriptPubKeyType)
	{
		var nativeScriptType = scriptPubKeyType switch
		{
			ScriptPubKeyType.Segwit => NativeScriptPubKeyType.Segwit,
			ScriptPubKeyType.TaprootBIP86 => NativeScriptPubKeyType.TaprootBIP86,
			_ => throw new NotSupportedException($"Script type {scriptPubKeyType} is not supported.")
		};

		var proofBytes = NativeOwnershipProof.Generate(
			key.ToBytes(),
			coinJoinInputsCommitmentData.ToBytes(),
			[ownershipIdentifier.ToBytes()],
			nativeScriptType,
			userConfirmation: true);

		return OwnershipProof.FromBytes(proofBytes);
	}

	public static bool VerifyCoinJoinInputProof(
		OwnershipProof ownershipProof,
		Script scriptPubKey,
		CoinJoinInputCommitmentData coinJoinInputsCommitmentData)
	{
		return NativeOwnershipProof.Verify(
			ownershipProof.ToBytes(),
			scriptPubKey.ToBytes(),
			coinJoinInputsCommitmentData.ToBytes(),
			requireUserConfirmation: true);
	}
}
