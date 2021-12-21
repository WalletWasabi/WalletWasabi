using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class StringCipherTests
	{
		[Theory]
		[InlineData("hellohellohellohellohello", "password")]
		[InlineData("01234567890123456789012345678901234567890123456789012345678901234567890123456789", "password")]
		[InlineData("foo@éóüö", "")]
		[InlineData("foo@éóüöhellohellohellohellohello", "passwordpassword3232")]
		public void CipherTests(string toEncrypt, string password)
		{
			var encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			var decrypted = StringCipher.Decrypt(encypted, password);
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
			Assert.True(rate is < 0.000001 and > (-0.000001));
		}
	}
}
