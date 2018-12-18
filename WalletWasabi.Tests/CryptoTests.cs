using System;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;
using Xunit;

namespace WalletWasabi.Tests
{
	public class CryptoTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public CryptoTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void CipherTests()
		{
			var toEncrypt = "hello";
			var password = "password";
			var encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			var decrypted = StringCipher.Decrypt(encypted, password);
			Assert.Equal(toEncrypt, decrypted);

			var builder = new StringBuilder();
			for (int i = 0; i < 1000000; i++) // check 10MB
			{
				builder.Append("0123456789");
			}

			toEncrypt = builder.ToString();
			encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			decrypted = StringCipher.Decrypt(encypted, password);
			Assert.Equal(toEncrypt, decrypted);

			toEncrypt = "foo@éóüö";
			password = "";
			encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			decrypted = StringCipher.Decrypt(encypted, password);
			Assert.Equal(toEncrypt, decrypted);
			Logger.TurnOff();
			Assert.Throws<CryptographicException>(() => StringCipher.Decrypt(encypted, "wrongpassword"));
			Logger.TurnOn();
		}

		[Fact]
		public void AuthenticateMessageTest()
		{
			var count = 0;
			var errorCount = 0;
			while (count < 3)
			{
				var password = "password";
				var plainText = "juan carlos";
				var encypted = StringCipher.Encrypt(plainText, password);

				try
				{
					// This must fail because the password is wrong
					var t = StringCipher.Decrypt(encypted, "WRONG-PASSWORD");
					errorCount++;
				}
				catch (CryptographicException ex)
				{
					Assert.StartsWith("Message Authentication failed", ex.Message);
				}
				count++;
			}
			var rate = (double)errorCount / (double)count;
			Assert.True(rate < 0.000001 && rate > -0.000001);
		}



/*
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

		[Fact]
		public void CanEncodeDecodeBlinding()
		{
			var key = new BlindingRsaKey();
			byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
			byte[] blindedData = key.PubKey.Blind(message).BlindedData;
			string encoded = ByteHelpers.ToHex(blindedData);
			byte[] decoded = ByteHelpers.FromHex(encoded);
			Assert.Equal(blindedData, decoded);
		}
*/
	}
}
