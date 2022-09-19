using NBitcoin;
using NBitcoin.DataEncoders;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class Bip322SignatureTest
{
	[Fact]
	public void P2wpkhBip322SignatureEncodingDecoding()
	{
		// [all all all all all all all all all all all all]/84'/0'/0'/1/0
		using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
		var message = new uint256(Encoders.Hex.DecodeData("BB06B6D65C9F4A1D245F203EB039A735D94D3ECA3E19B04DA59956413F5D5C8F"), false);
		var bip322Signature = Bip322Signature.Generate(key, message, ScriptPubKeyType.Segwit);

		var bip322SignatureBytes = Encoders.Hex.DecodeData("0002473044022006806E4B84C490372AD4728435DBF505FA39B4257E5A5B4A343A1C34A2D4390602205321627EF5933E8D2A589552ABE07D1EEE6F6C8C716DD5BA7F99C9F324694E230121032EF68318C8F6AAA0ADEC0199C69901F0DB7D3485EB38D9AD235221DC3D61154B");

		Assert.True(bip322SignatureBytes.SequenceEqual(bip322Signature.ToBytes()));
		var bip322SignatureDeserialized = Bip322Signature.FromBytes(bip322SignatureBytes);
		Assert.True(bip322SignatureBytes.SequenceEqual(bip322SignatureDeserialized.ToBytes()));
	}

	[Fact]
	public void P2wpkhBip322SignatureVerification()
	{
		// [all all all all all all all all all all all all]/84'/0'/0'/1/0
		using var key = new Key(Encoders.Hex.DecodeData("3460814214450E864EC722FF1F84F96C41746CD6BBE2F1C09B33972761032E9F"));
		var message = new uint256(Encoders.Hex.DecodeData("BB06B6D65C9F4A1D245F203EB039A735D94D3ECA3E19B04DA59956413F5D5C8F"), false);

		var bip322Signature = Bip322Signature.Generate(key, message, ScriptPubKeyType.Segwit);
		var signature = bip322Signature.Witness[0];
		var pubKey = bip322Signature.Witness[1];
		var scriptPubKey = key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);

		// Valid signature
		Assert.True(bip322Signature.Verify(message, scriptPubKey));

		byte[] invalidSignature;
		byte[] invalidPubKey;
		Bip322Signature invalidBip322Signature;

		// Invalid signature, modified scriptsig
		invalidBip322Signature = new Bip322Signature(new Script(new byte[] { 0x00 }), bip322Signature.Witness);
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));

		// Invalid signature, modified length field of signature in witness
		invalidSignature = signature.ToArray();
		invalidSignature[2] = 30;
		invalidBip322Signature = new Bip322Signature(bip322Signature.ScriptSig, new WitScript(new List<byte[]> { invalidSignature, pubKey }));
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));

		// Invalid signature, Modified r part of signature in witness
		invalidSignature = signature.ToArray();
		invalidSignature[4] ^= 1;
		invalidBip322Signature = new Bip322Signature(bip322Signature.ScriptSig, new WitScript(new List<byte[]> { invalidSignature, pubKey }));
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));

		// Invalid signature, Modified pubkey in witness
		invalidPubKey = pubKey.ToArray();
		invalidPubKey[2] ^= 1;
		invalidBip322Signature = new Bip322Signature(bip322Signature.ScriptSig, new WitScript(new List<byte[]> { signature, invalidPubKey }));
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));
	}

	[Fact]
	public void P2trBip322SignatureEncodingDecoding()
	{
		// [all all all all all all all all all all all all]/86'/0'/0'/1/0
		using var key = new Key(Encoders.Hex.DecodeData("E4E13C6ACF002C94D5EE25E89F419531A52A39F93B00B71349C4889E046BBA3C"));
		var message = new uint256(Encoders.Hex.DecodeData("BB06B6D65C9F4A1D245F203EB039A735D94D3ECA3E19B04DA59956413F5D5C8F"), false);
		var bip322Signature = Bip322Signature.Generate(key, message, ScriptPubKeyType.TaprootBIP86);

		var bip322SignatureBytes = Encoders.Hex.DecodeData("000140671520C782E52D070309579BA1FEE3889EB32D2E812A32B4899193233E5856E824014C17F68AB354BC4964AABBC957FE33AC557E2B3386923F3ED7A019DCC0C6");

		Assert.True(bip322SignatureBytes.SequenceEqual(bip322Signature.ToBytes()));
		var bip322SignatureDeserialized = Bip322Signature.FromBytes(bip322SignatureBytes);
		Assert.True(bip322SignatureBytes.SequenceEqual(bip322SignatureDeserialized.ToBytes()));
	}

	[Fact]
	public void P2trBip322SignatureVerification()
	{
		// [all all all all all all all all all all all all]/86'/0'/0'/1/0
		using var key = new Key(Encoders.Hex.DecodeData("E4E13C6ACF002C94D5EE25E89F419531A52A39F93B00B71349C4889E046BBA3C"));
		var message = new uint256(Encoders.Hex.DecodeData("BB06B6D65C9F4A1D245F203EB039A735D94D3ECA3E19B04DA59956413F5D5C8F"), false);

		var bip322Signature = Bip322Signature.Generate(key, message, ScriptPubKeyType.TaprootBIP86);
		var signature = bip322Signature.Witness[0];
		var scriptPubKey = key.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86);

		// Valid signature
		Assert.True(bip322Signature.Verify(message, scriptPubKey));

		byte[] invalidSignature;
		Bip322Signature invalidBip322Signature;

		// Invalid signature, modified scriptsig
		invalidBip322Signature = new Bip322Signature(new Script(new byte[] { 0x00 }), bip322Signature.Witness);
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));

		// Invalid signature, modified length field of signature in witness
		invalidSignature = signature.ToArray();
		invalidSignature[2] = 30;
		invalidBip322Signature = new Bip322Signature(bip322Signature.ScriptSig, new WitScript(new List<byte[]> { invalidSignature }));
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));

		// Invalid signature, Modified r part of signature in witness
		invalidSignature = signature.ToArray();
		invalidSignature[4] ^= 1;
		invalidBip322Signature = new Bip322Signature(bip322Signature.ScriptSig, new WitScript(new List<byte[]> { invalidSignature }));
		Assert.False(invalidBip322Signature.Verify(message, scriptPubKey));
	}
}
