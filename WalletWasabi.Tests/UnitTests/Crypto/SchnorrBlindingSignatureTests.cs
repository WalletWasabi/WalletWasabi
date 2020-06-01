using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Numerics;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class SchnorrBlindindSignatureTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanParseUnblindedSignature()
		{
			var requester = new SchnorrBlinding.Requester();
			var r = new Key(Encoders.Hex.DecodeData("31E151628AED2A6ABF7155809CF4F3C762E7160F38B4DA56B784D9045190CFA0"));
			var key = new Key(Encoders.Hex.DecodeData("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF"));
			var signer = new SchnorrBlinding.Signer(key);

			var message = new uint256(Encoders.Hex.DecodeData("243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89"), false);
			var blindedMessage = requester.BlindMessage(message, r.PubKey, key.PubKey);
			var blindSignature = signer.Sign(blindedMessage, r);
			var unblindedSignature = requester.UnblindSignature(blindSignature);

			var str = unblindedSignature.ToString();
			Assert.True(UnblindedSignature.TryParse(str, out var unblindedSignature2));
			Assert.Equal(unblindedSignature.C, unblindedSignature2.C);
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
			var requester = new SchnorrBlinding.Requester();
			var r = new Key(Encoders.Hex.DecodeData("31E151628AED2A6ABF7155809CF4F3C762E7160F38B4DA56B784D9045190CFA0"));
			var key = new Key(Encoders.Hex.DecodeData("B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF"));
			var signer = new SchnorrBlinding.Signer(key);

			var message = new uint256(Encoders.Hex.DecodeData("243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89"), false);
			var blindedMessage = requester.BlindMessage(message, r.PubKey, key.PubKey);
			var blindSignature = signer.Sign(blindedMessage, r);
			var unblindedSignature = requester.UnblindSignature(blindSignature);

			Assert.True(SchnorrBlinding.VerifySignature(message, unblindedSignature, key.PubKey));
			Assert.False(SchnorrBlinding.VerifySignature(uint256.Zero, unblindedSignature, key.PubKey));
			Assert.False(SchnorrBlinding.VerifySignature(uint256.One, unblindedSignature, key.PubKey));

			// Test with unknown values 
			requester = new SchnorrBlinding.Requester();
			signer = new SchnorrBlinding.Signer(new Key());

			message = NBitcoin.Crypto.Hashes.Hash256(Encoders.ASCII.DecodeData("Hello world!"));
			blindedMessage = requester.BlindMessage(message, r.PubKey, signer.Key.PubKey);

			blindSignature = signer.Sign(blindedMessage, r);
			unblindedSignature = requester.UnblindSignature(blindSignature);
			Assert.True(SchnorrBlinding.VerifySignature(message, unblindedSignature, signer.Key.PubKey));
			Assert.False(SchnorrBlinding.VerifySignature(uint256.One, unblindedSignature, signer.Key.PubKey));
			Assert.False(SchnorrBlinding.VerifySignature(uint256.One, unblindedSignature, signer.Key.PubKey));
			var newMessage = Encoders.ASCII.DecodeData("Hello, World!");
			for (var i = 0; i < 1_000; i++)
			{
				requester = new SchnorrBlinding.Requester();
				signer = new SchnorrBlinding.Signer(new Key());
				blindedMessage = requester.BlindMessage(newMessage, r.PubKey, signer.Key.PubKey);
				blindSignature = signer.Sign(blindedMessage, r);
				unblindedSignature = requester.UnblindSignature(blindSignature);

				Assert.True(signer.VerifyUnblindedSignature(unblindedSignature, newMessage));
			}

			var ex = Assert.Throws<ArgumentException>(() => signer.Sign(uint256.Zero, r));
			Assert.StartsWith("Invalid blinded message.", ex.Message);
		}
	}
}