using HiddenWallet.Crypto;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

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
			var blindingResult = key.PubKey.Blind(message);

			// sign the blinded message
			var signature = key.SignBlindedData(blindingResult.BlindedData);

			// unblind the signature
			var unblindedSignature = key.PubKey.UnblindSignature(signature, blindingResult.BlindingFactor);

			// verify the original data is signed
			Assert.True(key.PubKey.Verify(unblindedSignature, message));
		}

		[Fact]
		public void CanEncodeDecode()
		{
			var key = new BlindingRsaKey();
			byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");

			byte[] blindedData = key.PubKey.Blind(message).BlindedData;
			string encoded = HexHelpers.ToString(blindedData);
			byte[] decoded = HexHelpers.GetBytes(encoded);

			Assert.Equal(blindedData, decoded);
		}
	}
}
