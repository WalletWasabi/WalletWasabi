using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Linq;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class OwnershipProofTest
	{
		[Fact]
		public void OwnershipProofEncodingDecoding()
		{
			// Trezor test vector
			// [all all all all all all all all all all all all]/84'/0'/0'/1/0
			using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
			var ownershipIdentifier = new OwnershipIdentifier(Encoders.Hex.DecodeData("A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD5707"));
			var commitmentData = Array.Empty<byte>();
			var ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, false, ScriptPubKeyType.Segwit);
			var scriptPubKey = PayToWitPubKeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey);

			var ownershipProofBytes = Encoders.Hex.DecodeData("534c00190001a122407efc198211c81af4450f40b235d54775efd934d16b9e31c6ce9bad57070002483045022100e5eaf2cb0a473b4545115c7b85323809e75cb106175ace38129fd62323d73df30220363dbc7acb7afcda022b1f8d97acb8f47c42043cfe0595583aa26e30bc8b3bb50121032ef68318c8f6aaa0adec0199c69901f0db7d3485eb38d9ad235221dc3d61154b");

			Assert.True(ownershipProofBytes.SequenceEqual(ownershipProof.ToBytes()));
			var deserializedOwnershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
			Assert.True(ownershipProofBytes.SequenceEqual(deserializedOwnershipProof.ToBytes()));
		}

		[Fact]
		public void OwnershipProofVerification()
		{
			// [all all all all all all all all all all all all]/84'/0'/0'/1/0
			using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
			var ownershipIdentifier = new OwnershipIdentifier(Encoders.Hex.DecodeData("A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD5707"));
			var commitmentData = Encoders.Hex.DecodeData("A42E38EF564D4B05B65575D22553BB1F264332D77F8A61159ABF3E6179B0317C");
			var scriptPubKey = PayToWitPubKeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey);

			OwnershipProof ownershipProof, invalidOwnershipProof;

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
			var invalidKey = new Key(Encoders.Hex.DecodeData("7b041dd735e7202d3c1b9592147894ed24da6355f0cd66573c273c0df1afa78a"));
			var invalidScriptPubKey = PayToWitPubKeyHashTemplate.Instance.GenerateScriptPubKey(invalidKey.PubKey);
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
}
