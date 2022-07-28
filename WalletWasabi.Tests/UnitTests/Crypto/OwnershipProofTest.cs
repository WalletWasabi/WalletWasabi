using NBitcoin;
using NBitcoin.DataEncoders;
using System.Linq;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class OwnershipProofTest
{
	[Fact]
	public void OwnershipProofEncodingDecoding()
	{
		// SLIP-19 test vector
		// See https://github.com/satoshilabs/slips/blob/846a0a6c1dfc29f8b90fd90a9309b1174b7d91e8/slip-0019.md#test-vector-1-p2wpkh
		// [all all all all all all all all all all all all]/84'/0'/0'/1/0
		var allMnemonic = new Mnemonic("all all all all all all all all all all all all");
		var identificationMasterKey = Slip21Node.FromSeed(allMnemonic.DeriveSeed());
		var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019").DeriveChild("Ownership identification key").Key;
		using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));

		var ownershipIdentifier = new OwnershipIdentifier(identificationKey, key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
		var ownershipIdentifierBytes = Encoders.Hex.DecodeData("A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD5707");
		Assert.True(ownershipIdentifierBytes.SequenceEqual(ownershipIdentifier.ToBytes()));

		var commitmentData = Array.Empty<byte>();
		var ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, false, ScriptPubKeyType.Segwit);
		var scriptPubKey = key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);

		var ownershipProofBytes = Encoders.Hex.DecodeData("534c00190001a122407efc198211c81af4450f40b235d54775efd934d16b9e31c6ce9bad57070002483045022100c0dc28bb563fc5fea76cacff75dba9cb4122412faae01937cdebccfb065f9a7002202e980bfbd8a434a7fc4cd2ca49da476ce98ca097437f8159b1a386b41fcdfac50121032ef68318c8f6aaa0adec0199c69901f0db7d3485eb38d9ad235221dc3d61154b");

		Assert.True(ownershipProofBytes.SequenceEqual(ownershipProof.ToBytes()));
		var deserializedOwnershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
		Assert.True(ownershipProofBytes.SequenceEqual(deserializedOwnershipProof.ToBytes()));
	}

	[Fact]
	public void OwnershipProofVerification()
	{
		OwnershipProof ownershipProof, invalidOwnershipProof;
		Script scriptPubKey;
		byte[] commitmentData;

		// SLIP-19 test vector
		// See https://github.com/satoshilabs/slips/blob/846a0a6c1dfc29f8b90fd90a9309b1174b7d91e8/slip-0019.md#test-vector-1-p2wpkh
		commitmentData = Encoders.Hex.DecodeData("");
		scriptPubKey = Script.FromHex("0014B2F771C370CCF219CD3059CDA92BDF7F00CF2103");
		var ownershipProofBytes = Encoders.Hex.DecodeData("534C00190001A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD57070002483045022100C0DC28BB563FC5FEA76CACFF75DBA9CB4122412FAAE01937CDEBCCFB065F9A7002202E980BFBD8A434A7FC4CD2CA49DA476CE98CA097437F8159B1A386B41FCDFAC50121032EF68318C8F6AAA0ADEC0199C69901F0DB7D3485EB38D9AD235221DC3D61154B");
		ownershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));

		// [all all all all all all all all all all all all]/84'/0'/0'/1/0
		using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
		var ownershipIdentifier = new OwnershipIdentifier(Encoders.Hex.DecodeData("A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD5707"));
		commitmentData = Encoders.Hex.DecodeData("A42E38EF564D4B05B65575D22553BB1F264332D77F8A61159ABF3E6179B0317C");
		scriptPubKey = key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);

		// Valid proofs
		ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, true, ScriptPubKeyType.Segwit);
		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, true));

		ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, false, ScriptPubKeyType.Segwit);
		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));

		// Valid proof, modified user confirmation flag
		Assert.False(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, true));

		// Valid proof, modified commitment data
		var invalidCommitmentData = commitmentData.ToArray();
		invalidCommitmentData[0] ^= 1;
		Assert.False(ownershipProof.VerifyOwnership(scriptPubKey, invalidCommitmentData, false));

		// Valid proof, invalid scriptPubKey
		// [all all all all all all all all all all all all]/84'/0'/0'/1/1
		using var invalidKey = new Key(Encoders.Hex.DecodeData("7b041dd735e7202d3c1b9592147894ed24da6355f0cd66573c273c0df1afa78a"));
		var invalidScriptPubKey = invalidKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);

		Assert.False(ownershipProof.VerifyOwnership(invalidScriptPubKey, commitmentData, false));

		// Invalid proof, modified ownership identifier
		var invalidOwnershipIdentifierBytes = ownershipIdentifier.ToBytes();
		invalidOwnershipIdentifierBytes[0] ^= 1;

		invalidOwnershipProof = new OwnershipProof(
			new ProofBody(ownershipProof.ProofBody.Flags, new OwnershipIdentifier(invalidOwnershipIdentifierBytes)),
			ownershipProof.ProofSignature);

		Assert.False(invalidOwnershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));

		// Invalid proof, modified r part of signature in witness
		var invalidSignature = ownershipProof.ProofSignature.Witness[0].ToArray();
		invalidSignature[4] ^= 1;

		invalidOwnershipProof = new OwnershipProof(
			ownershipProof.ProofBody,
			new Bip322Signature(ownershipProof.ProofSignature.ScriptSig, new WitScript(new byte[][] { invalidSignature, ownershipProof.ProofSignature.Witness[1] })));

		Assert.False(invalidOwnershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
	}
}
