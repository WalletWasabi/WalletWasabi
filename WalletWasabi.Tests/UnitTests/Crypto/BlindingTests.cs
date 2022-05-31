using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.JsonConverters;
using Xunit;
using static WalletWasabi.Crypto.SchnorrBlinding;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class BlindingTests
{
	private static Random Random = new(123456);

	[Fact]
	[Trait("UnitTest", "UnitTest")]
	public void CanParseUnblindedSignature()
	{
		var requester = new Requester();
		using var r = new Key(Encoders.Hex.DecodeData("31E151628AED2A6ABF7155809CF4F3C762E7160F38B4DA56B784D9045190CFA0"));
		using var key = new Key(Encoders.Hex.DecodeData("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF"));
		var signer = new Signer(key);

		var message = new uint256(Encoders.Hex.DecodeData("243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89"), false);
		var blindedMessage = requester.BlindMessage(message, r.PubKey, key.PubKey);
		var blindSignature = signer.Sign(blindedMessage, r);
		var unblindedSignature = requester.UnblindSignature(blindSignature);

		var str = unblindedSignature.ToString();
		Assert.True(UnblindedSignature.TryParse(str, out var unblindedSignature2));
		Assert.Equal(unblindedSignature.C, unblindedSignature2!.C);
		Assert.Equal(unblindedSignature.S, unblindedSignature2.S);
		str += "o";
		Assert.False(UnblindedSignature.TryParse(str, out _));
		Assert.Throws<FormatException>(() => UnblindedSignature.Parse(str));
		byte[] overflow = new byte[64];
		overflow.AsSpan().Fill(255);
		Assert.False(UnblindedSignature.TryParse(overflow, out _));
		Assert.Throws<FormatException>(() => UnblindedSignature.Parse(overflow));
	}

	[Fact]
	[Trait("UnitTest", "UnitTest")]
	public void BlindingSignature()
	{
		// Test with known values
		var requester = new Requester();
		using var r = new Key(Encoders.Hex.DecodeData("31E151628AED2A6ABF7155809CF4F3C762E7160F38B4DA56B784D9045190CFA0"));
		using var key = new Key(Encoders.Hex.DecodeData("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF"));
		var signer = new Signer(key);

		var message = new uint256(Encoders.Hex.DecodeData("243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89"), false);
		var blindedMessage = requester.BlindMessage(message, r.PubKey, key.PubKey);
		var blindSignature = signer.Sign(blindedMessage, r);
		var unblindedSignature = requester.UnblindSignature(blindSignature);

		Assert.True(VerifySignature(message, unblindedSignature, key.PubKey));
		Assert.False(VerifySignature(uint256.Zero, unblindedSignature, key.PubKey));
		Assert.False(VerifySignature(uint256.One, unblindedSignature, key.PubKey));

		// Test with unknown values
		requester = new Requester();
		using var k = new Key();
		signer = new Signer(k);

		message = NBitcoin.Crypto.Hashes.DoubleSHA256(Encoders.ASCII.DecodeData("Hello world!"));
		blindedMessage = requester.BlindMessage(message, r.PubKey, signer.Key.PubKey);

		blindSignature = signer.Sign(blindedMessage, r);
		unblindedSignature = requester.UnblindSignature(blindSignature);
		Assert.True(VerifySignature(message, unblindedSignature, signer.Key.PubKey));
		Assert.False(VerifySignature(uint256.One, unblindedSignature, signer.Key.PubKey));
		Assert.False(VerifySignature(uint256.One, unblindedSignature, signer.Key.PubKey));
		var newMessage = Encoders.ASCII.DecodeData("Hello, World!");
		for (var i = 0; i < 1_000; i++)
		{
			requester = new Requester();
			using var k2 = new Key();
			signer = new Signer(k2);
			blindedMessage = requester.BlindMessage(newMessage, r.PubKey, signer.Key.PubKey);
			blindSignature = signer.Sign(blindedMessage, r);
			unblindedSignature = requester.UnblindSignature(blindSignature);

			Assert.True(signer.VerifyUnblindedSignature(unblindedSignature, newMessage));
		}

		var ex = Assert.Throws<ArgumentException>(() => signer.Sign(uint256.Zero, r));
		Assert.StartsWith("Invalid blinded message.", ex.Message);
	}

	[Fact]
	public void CanBlindSign()
	{
		// Generate ECDSA keypairs.
		using var r = new Key();
		using var key = new Key();
		Signer signer = new(key);

		// Generate ECDSA requester.
		// Get the r's pubkey and the key's pubkey.
		// Blind messages.
		Requester requester = new();
		PubKey rPubKey = r.PubKey;
		PubKey keyPubKey = key.PubKey;

		byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
		byte[] hashBytes = NBitcoin.Crypto.Hashes.SHA256(message);
		uint256 hash = new(hashBytes);
		uint256 blindedMessageHash = requester.BlindMessage(hash, rPubKey, keyPubKey);

		// Sign the blinded message hash.
		uint256 blindedSignature = signer.Sign(blindedMessageHash, r);

		// Unblind the signature.
		UnblindedSignature unblindedSignature = requester.UnblindSignature(blindedSignature);

		// Verify the original data is signed.

		Assert.True(VerifySignature(hash, unblindedSignature, keyPubKey));
	}

	[Fact]
	public void CanEncodeDecodeBlinding()
	{
		using var key = new Key();
		using var r = new Key();
		byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
		var hash = new uint256(NBitcoin.Crypto.Hashes.SHA256(message));
		var requester = new Requester();
		uint256 blindedHash = requester.BlindMessage(hash, r.PubKey, key.PubKey);
		string encoded = blindedHash.ToString();
		uint256 decoded = new(encoded);
		Assert.Equal(blindedHash, decoded);
	}

	[Fact]
	public void ConvertBackAndForth()
	{
		var converter = new UnblindedSignatureJsonConverter();
		using var r = new Key();
		using var key = new Key();
		var signer = new Signer(key);

		foreach (int _ in Enumerable.Range(0, 100))
		{
			var requester = new Requester();

			var message = new byte[256];
			Random.NextBytes(message);
			var blindedMessage = requester.BlindMessage(message, r.PubKey, key.PubKey);
			var blindSignature = signer.Sign(blindedMessage, r);
			UnblindedSignature unblindedSignature = requester.UnblindSignature(blindSignature);

			string json = JsonConvert.SerializeObject(unblindedSignature, converter);
			UnblindedSignature convertedUnblindedSignature = JsonConvert.DeserializeObject<UnblindedSignature>(json, converter)!;

			Assert.NotNull(convertedUnblindedSignature);
			Assert.Equal(unblindedSignature.C, convertedUnblindedSignature.C);
			Assert.Equal(unblindedSignature.S, convertedUnblindedSignature.S);
		}
	}

	[Fact]
	public void DetectInvalidSerializedMessage()
	{
		UnblindedSignatureJsonConverter converter = new();
		string json = "[ '999999999999999999999999999999999999999999999999999999999999999999999999999999'," + // 33 bytes (INVALID)
					" '999999999999999999999999999']";

		FormatException ex = Assert.Throws<FormatException>(() => JsonConvert.DeserializeObject<UnblindedSignature>(json, converter));
		Assert.Contains("longer than 32 bytes", ex.Message);
	}
}
