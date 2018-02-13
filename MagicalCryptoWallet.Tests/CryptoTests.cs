using MagicalCryptoWallet.Crypto;
using MagicalCryptoWallet.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace MagicalCryptoWallet.Tests
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
			Assert.Throws<CryptographicException>(() => StringCipher.Decrypt(encypted, ""));
			Assert.Throws<CryptographicException>(() => StringCipher.Decrypt(encypted, "wrongpassword"));

			toEncrypt = "foo@éóüö";
			password = "";
			encypted = StringCipher.Encrypt(toEncrypt, password);
			Assert.NotEqual(toEncrypt, encypted);
			decrypted = StringCipher.Decrypt(encypted, password);
			Assert.Equal(toEncrypt, decrypted);
			Assert.Throws<CryptographicException>(() => StringCipher.Decrypt(encypted, "wrongpassword"));
		}
	}
}
