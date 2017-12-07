using HiddenWallet.Crypto;
using NBitcoin;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Diagnostics;

namespace HiddenWallet.Tests
{
    public class CryptoTests
    {
		[Fact]
		public void CanBlindSign()
		{
			// generate rsa keypair
			var key = new BlindingRsaKey();

			// generate blinding factor with pubkey
			// blind message
			byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
			var (BlindingFactor, BlindedData) = key.PubKey.Blind(message);

			// sign the blinded message
			var signature = key.SignBlindedData(BlindedData);

			// unblind the signature
			var unblindedSignature = key.PubKey.UnblindSignature(signature, BlindingFactor);

			// verify the original data is signed
			Assert.True(key.PubKey.Verify(unblindedSignature, message));
		}

		[Fact]
		public void CanSerialize()
		{
			var key = new BlindingRsaKey();
			string jsonKey = key.ToJson();
			var key2 = BlindingRsaKey.CreateFromJson(jsonKey);

			Assert.Equal(key, key2);
			Assert.Equal(key.PubKey, key2.PubKey);

			var jsonPubKey = key.PubKey.ToJson();
			var pubKey2 = BlindingRsaPubKey.CreateFromJson(jsonPubKey);
			Assert.Equal(key.PubKey, pubKey2);

			// generate blinding factor with pubkey
			// blind message
			byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
			var (BlindingFactor, BlindedData) = pubKey2.Blind(message);

			// sign the blinded message
			var signature = key.SignBlindedData(BlindedData);

			// unblind the signature
			var unblindedSignature = key2.PubKey.UnblindSignature(signature, BlindingFactor);

			// verify the original data is signed
			Assert.True(key2.PubKey.Verify(unblindedSignature, message));
		}
	}
}
