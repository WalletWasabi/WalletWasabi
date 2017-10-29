using HiddenWallet.Crypto;
using NBitcoin;
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
	}
}
