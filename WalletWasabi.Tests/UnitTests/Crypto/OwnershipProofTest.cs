// using NBitcoin;
// using NBitcoin.DataEncoders;
// using System.Linq;
// using System.Text;
// using WalletWasabi.Crypto;
// using Xunit;
//
// namespace WalletWasabi.Tests.UnitTests.Crypto;
//
// public class OwnershipProofTest
// {
// 	[Fact]
// 	public void P2wpkhOwnershipProofEncodingDecoding()
// 	{
// 		// SLIP-19 test vector
// 		// See https://github.com/satoshilabs/slips/blob/846a0a6c1dfc29f8b90fd90a9309b1174b7d91e8/slip-0019.md#test-vector-1-p2wpkh
// 		// [all all all all all all all all all all all all]/84'/0'/0'/1/0
// 		var allMnemonic = new Mnemonic("all all all all all all all all all all all all");
// 		var identificationMasterKey = Slip21Node.FromSeed(allMnemonic.DeriveSeed());
// 		var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019").DeriveChild("Ownership identification key").Key;
// 		using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
//
// 		var ownershipIdentifier = new OwnershipIdentifier(identificationKey, key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
// 		var ownershipIdentifierBytes = Encoders.Hex.DecodeData("A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD5707");
// 		Assert.True(ownershipIdentifierBytes.SequenceEqual(ownershipIdentifier.ToBytes()));
//
// 		var commitmentData = Array.Empty<byte>();
//
// 		var ownershipProof = OwnershipProof.Generate(key, key.GetAddress(ScriptPubKeyType.Segwit, Network.Main),
// 			Encoding.UTF8.GetString(commitmentData));
// 		var ownershipProofBytes = Encoders.Hex.DecodeData("534C00190001A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD57070002483045022100C0DC28BB563FC5FEA76CACFF75DBA9CB4122412FAAE01937CDEBCCFB065F9A7002202E980BFBD8A434A7FC4CD2CA49DA476CE98CA097437F8159B1A386B41FCDFAC50121032EF68318C8F6AAA0ADEC0199C69901F0DB7D3485EB38D9AD235221DC3D61154B");
//
// 		Assert.True(ownershipProofBytes.SequenceEqual(ownershipProof.ToBytes()));
// 		var deserializedOwnershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
// 		Assert.True(ownershipProofBytes.SequenceEqual(deserializedOwnershipProof.ToBytes()));
// 	}
//
// 	[Fact]
// 	public void P2wpkhOwnershipProofVerification()
// 	{
// 		OwnershipProof ownershipProof, invalidOwnershipProof;
// 		Script scriptPubKey;
// 		byte[] commitmentData;
//
// 		// SLIP-19 test vector
// 		// See https://github.com/satoshilabs/slips/blob/846a0a6c1dfc29f8b90fd90a9309b1174b7d91e8/slip-0019.md#test-vector-1-p2wpkh
// 		commitmentData = Encoders.Hex.DecodeData("");
// 		scriptPubKey = Script.FromHex("0014B2F771C370CCF219CD3059CDA92BDF7F00CF2103");
// 		var ownershipProofBytes = Encoders.Hex.DecodeData("534C00190001A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD57070002483045022100C0DC28BB563FC5FEA76CACFF75DBA9CB4122412FAAE01937CDEBCCFB065F9A7002202E980BFBD8A434A7FC4CD2CA49DA476CE98CA097437F8159B1A386B41FCDFAC50121032EF68318C8F6AAA0ADEC0199C69901F0DB7D3485EB38D9AD235221DC3D61154B");
// 		ownershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
//
// 		// [all all all all all all all all all all all all]/84'/0'/0'/1/0
// 		using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
// 		var ownershipIdentifier = new OwnershipIdentifier(Encoders.Hex.DecodeData("A122407EFC198211C81AF4450F40B235D54775EFD934D16B9E31C6CE9BAD5707"));
// 		commitmentData = Encoders.Hex.DecodeData("A42E38EF564D4B05B65575D22553BB1F264332D77F8A61159ABF3E6179B0317C");
// 		scriptPubKey = key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
//
// 		// Valid proofs
// 		ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, true, ScriptPubKeyType.Segwit);
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, true));
//
// 		ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, false, ScriptPubKeyType.Segwit);
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
//
// 		// Valid proof, modified user confirmation flag
// 		Assert.False(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, true));
//
// 		// Valid proof, modified commitment data
// 		var invalidCommitmentData = commitmentData.ToArray();
// 		invalidCommitmentData[0] ^= 1;
// 		Assert.False(ownershipProof.VerifyOwnership(scriptPubKey, invalidCommitmentData, false));
//
// 		// Valid proof, invalid scriptPubKey
// 		// [all all all all all all all all all all all all]/84'/0'/0'/1/1
// 		using var invalidKey = new Key(Encoders.Hex.DecodeData("7B041DD735E7202D3C1B9592147894ED24DA6355F0CD66573C273C0DF1AFA78A"));
// 		var invalidScriptPubKey = invalidKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
//
// 		Assert.False(ownershipProof.VerifyOwnership(invalidScriptPubKey, commitmentData, false));
//
// 		// Invalid proof, modified ownership identifier
// 		var invalidOwnershipIdentifierBytes = ownershipIdentifier.ToBytes();
// 		invalidOwnershipIdentifierBytes[0] ^= 1;
//
// 		invalidOwnershipProof = new OwnershipProof(
// 			new ProofBody(ownershipProof.ProofBody.Flags, new OwnershipIdentifier(invalidOwnershipIdentifierBytes)),
// 			ownershipProof.ProofSignature);
//
// 		Assert.False(invalidOwnershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
//
// 		// Invalid proof, modified r part of signature in witness
// 		var invalidSignature = ownershipProof.ProofSignature.Witness[0].ToArray();
// 		invalidSignature[4] ^= 1;
//
// 		invalidOwnershipProof = new OwnershipProof(
// 			ownershipProof.ProofBody,
// 			new Bip322Signature(ownershipProof.ProofSignature.ScriptSig, new WitScript(new byte[][] { invalidSignature, ownershipProof.ProofSignature.Witness[1] })));
//
// 		Assert.False(invalidOwnershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
// 	}
//
// 	[Fact]
// 	public void P2trOwnershipProofEncodingDecoding()
// 	{
// 		// SLIP-19 test vector
// 		// See https://github.com/satoshilabs/slips/blob/846a0a6c1dfc29f8b90fd90a9309b1174b7d91e8/slip-0019.md#test-vector-5-p2tr
// 		// [all all all all all all all all all all all all]/86'/0'/0'/1/0
// 		var allMnemonic = new Mnemonic("all all all all all all all all all all all all");
// 		var identificationMasterKey = Slip21Node.FromSeed(allMnemonic.DeriveSeed());
// 		var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019").DeriveChild("Ownership identification key").Key;
// 		using var key = new Key(Encoders.Hex.DecodeData("E4E13C6ACF002C94D5EE25E89F419531A52A39F93B00B71349C4889E046BBA3C"));
//
// 		var ownershipIdentifier = new OwnershipIdentifier(identificationKey, key.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86));
// 		var ownershipIdentifierBytes = Encoders.Hex.DecodeData("DC18066224B9E30E306303436DC18AB881C7266C13790350A3FE415E438135EC");
// 		Assert.True(ownershipIdentifierBytes.SequenceEqual(ownershipIdentifier.ToBytes()));
//
// 		var commitmentData = Array.Empty<byte>();
// 		var ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, false, ScriptPubKeyType.TaprootBIP86);
// 		var ownershipProofBytes = Encoders.Hex.DecodeData("534C00190001DC18066224B9E30E306303436DC18AB881C7266C13790350A3FE415E438135EC000140647D6AF883107A870417E808ABE424882BD28EE04A28BA85A7E99400E1B9485075733695964C2A0FA02D4439AB80830E9566CCBD10F2597F5513EFF9F03A0497");
//
// 		Assert.True(ownershipProofBytes.SequenceEqual(ownershipProof.ToBytes()));
// 		var deserializedOwnershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
// 		Assert.True(ownershipProofBytes.SequenceEqual(deserializedOwnershipProof.ToBytes()));
// 	}
//
// 	[Fact]
// 	public void P2trOwnershipProofVerification()
// 	{
// 		OwnershipProof ownershipProof, invalidOwnershipProof;
// 		Script scriptPubKey;
// 		byte[] commitmentData;
//
// 		// SLIP-19 test vector
// 		// See https://github.com/satoshilabs/slips/blob/846a0a6c1dfc29f8b90fd90a9309b1174b7d91e8/slip-0019.md#test-vector-5-p2tr
// 		commitmentData = Encoders.Hex.DecodeData("");
// 		scriptPubKey = Script.FromHex("51204102897557DE0CAFEA0A8401EA5B59668ECCB753E4B100AEBE6A19609F3CC79F");
// 		var ownershipProofBytes = Encoders.Hex.DecodeData("534C00190001DC18066224B9E30E306303436DC18AB881C7266C13790350A3FE415E438135EC0001401B553E5B9CC787B531BBC78417AEA901272B4EA905136A2BABC4D6CA471549743B5E0E39DDC14E620B254E42FAA7F6D5BD953E97AA231D764D21BC5A58E8B7D9");
// 		ownershipProof = OwnershipProof.FromBytes(ownershipProofBytes);
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
//
// 		// [all all all all all all all all all all all all]/86'/0'/0'/1/0
// 		using var key = new Key(Encoders.Hex.DecodeData("E4E13C6ACF002C94D5EE25E89F419531A52A39F93B00B71349C4889E046BBA3C"));
// 		var ownershipIdentifier = new OwnershipIdentifier(Encoders.Hex.DecodeData("DC18066224B9E30E306303436DC18AB881C7266C13790350A3FE415E438135EC"));
// 		commitmentData = Encoders.Hex.DecodeData("A42E38EF564D4B05B65575D22553BB1F264332D77F8A61159ABF3E6179B0317C");
// 		scriptPubKey = key.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86);
//
// 		// Valid proofs
// 		ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, true, ScriptPubKeyType.TaprootBIP86);
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, true));
//
// 		ownershipProof = OwnershipProof.Generate(key, ownershipIdentifier, commitmentData, false, ScriptPubKeyType.TaprootBIP86);
// 		Assert.True(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
//
// 		// Valid proof, modified user confirmation flag
// 		Assert.False(ownershipProof.VerifyOwnership(scriptPubKey, commitmentData, true));
//
// 		// Valid proof, modified commitment data
// 		var invalidCommitmentData = commitmentData.ToArray();
// 		invalidCommitmentData[0] ^= 1;
// 		Assert.False(ownershipProof.VerifyOwnership(scriptPubKey, invalidCommitmentData, false));
//
// 		// Valid proof, invalid scriptPubKey
// 		// [all all all all all all all all all all all all]/84'/0'/0'/1/1
// 		using var invalidKey = new Key(Encoders.Hex.DecodeData("7B041DD735E7202D3C1B9592147894ED24DA6355F0CD66573C273C0DF1AFA78A"));
// 		var invalidScriptPubKey = invalidKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86);
// 		Assert.False(ownershipProof.VerifyOwnership(invalidScriptPubKey, commitmentData, false));
//
// 		// Invalid proof, modified ownership identifier
// 		var invalidOwnershipIdentifierBytes = ownershipIdentifier.ToBytes();
// 		invalidOwnershipIdentifierBytes[0] ^= 1;
//
// 		invalidOwnershipProof = new OwnershipProof(
// 			new ProofBody(ownershipProof.ProofBody.Flags, new OwnershipIdentifier(invalidOwnershipIdentifierBytes)),
// 			ownershipProof.ProofSignature);
//
// 		Assert.False(invalidOwnershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
//
// 		// Invalid proof, modified r part of signature in witness
// 		var invalidSignature = ownershipProof.ProofSignature.Witness[0].ToArray();
// 		invalidSignature[4] ^= 1;
//
// 		invalidOwnershipProof = new OwnershipProof(
// 			ownershipProof.ProofBody,
// 			new Bip322Signature(ownershipProof.ProofSignature.ScriptSig, new WitScript(new byte[][] { invalidSignature })));
//
// 		Assert.False(invalidOwnershipProof.VerifyOwnership(scriptPubKey, commitmentData, false));
// 	}
// }
