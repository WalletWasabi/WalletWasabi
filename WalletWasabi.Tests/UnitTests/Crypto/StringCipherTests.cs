using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class StringCipherTests
	{

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
			Assert.True(rate is < 0.000001 and > (-0.000001));
		}

		[Fact]
		public void CipherTests()
		{
			var password = "password";

			var builder = new StringBuilder();
			for (int i = 0; i < 1000000; i++) // check 10MB
			{
				builder.Append("0123456789");
			}

			var toEncrypt = builder.ToString();
			var encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			var decrypted = StringCipher.Decrypt(encypted, password);
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
		public void LongTextWithPartialReadTests()
		{
			var toEncrypt = "hello world, how are you doing todays?";
			var password = "password";
			var encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			var decrypted = StringCipher.Decrypt(encypted, password);
			Assert.Equal(toEncrypt, decrypted);
		}
	}
}
