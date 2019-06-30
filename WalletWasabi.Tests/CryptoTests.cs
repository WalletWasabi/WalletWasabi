using NBitcoin;
using NBitcoin.Crypto;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using Xunit;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.Tests
{
	public class CryptoTests
	{
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
			var rate = errorCount / (double)count;
			Assert.True(rate < 0.000001 && rate > -0.000001);
		}

		[Fact]
		public void CanBlindSign()
		{
			// Generate ECDSA keypairs.
			var r = new Key();
			var key = new Key();
			Signer signer = new Signer(key, r);

			// Generate ECDSA requester.
			// Get the r's pubkey and the key's pubkey.
			// Blind messages.
			Requester requester = new Requester();
			PubKey rPubkey = r.PubKey;
			PubKey keyPubkey = key.PubKey;

			byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
			byte[] hashBytes = Hashes.SHA256(message);
			uint256 hash = new uint256(hashBytes);
			uint256 blindedMessageHash = requester.BlindMessage(hash, rPubkey, keyPubkey);

			// Sign the blinded message hash.
			uint256 blindedSignature = signer.Sign(blindedMessageHash);

			// Unblind the signature.
			UnblindedSignature unblindedSignature = requester.UnblindSignature(blindedSignature);

			// verify the original data is signed

			Assert.True(VerifySignature(hash, unblindedSignature, keyPubkey));
		}

		[Fact]
		public void CanEncodeDecodeBlinding()
		{
			var key = new Key();
			var r = new Key();
			byte[] message = Encoding.UTF8.GetBytes("áéóúősing me please~!@#$%^&*())_+");
			var hash = new uint256(Hashes.SHA256(message));
			var requester = new Requester();
			uint256 blindedHash = requester.BlindMessage(hash, r.PubKey, key.PubKey);
			string encoded = blindedHash.ToString();
			uint256 decoded = new uint256(encoded);
			Assert.Equal(blindedHash, decoded);
		}
	}
}
