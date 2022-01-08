using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class StringCipherTests
{
	/// <summary>
	/// Tests that we can decrypt encrypted text correctly.
	/// </summary>
	[Theory]
	[InlineData("hello", "password")]
	[InlineData("hello world, how are you doing today?", "password")]
	[InlineData("01234567890123456789012345678901234567890123456789012345678901234567890123456789", "password")]
	[InlineData("foo@éóüö", "")]
	[InlineData("foo@éóüöhellohellohellohellohello", "passwordpassword3232")]
	public void RoundTripCipherTests(string toEncrypt, string password)
	{
		string encrypted = StringCipher.Encrypt(toEncrypt, password);
		Assert.NotEqual(toEncrypt, encrypted);

		string decrypted = StringCipher.Decrypt(encrypted, password);
		Assert.Equal(toEncrypt, decrypted);
	}

	[Fact]
	public void EncryptLongTextTest()
	{
		StringBuilder builder = new();

		for (int i = 0; i < 1000000; i++) // 10MB
		{
			builder.Append("0123456789");
		}

		string password = "password";
		string toEncrypt = builder.ToString();
		string encrypted = StringCipher.Encrypt(toEncrypt, password);
		Assert.NotEqual(toEncrypt, encrypted);

		string decrypted = StringCipher.Decrypt(encrypted, password);
		Assert.Equal(toEncrypt, decrypted);
	}

	[Fact]
	public void InvalidDecryptTest()
	{
		string encrypted = StringCipher.Encrypt("123456789", "password");
		Assert.Throws<CryptographicException>(() => StringCipher.Decrypt(encrypted, "wrong-password"));
	}

	[Fact]
	public void AuthenticateMessageTest()
	{
		int count = 0;
		int errorCount = 0;

		while (count < 3)
		{
			string password = "password";
			string plainText = "juan carlos";
			string encrypted = StringCipher.Encrypt(plainText, password);

			try
			{
				// This must fail because the password is wrong
				var t = StringCipher.Decrypt(encrypted, "WRONG-PASSWORD");
				errorCount++;
			}
			catch (CryptographicException ex)
			{
				Assert.StartsWith("Message Authentication failed", ex.Message);
			}

			count++;
		}

		double rate = errorCount / (double)count;
		Assert.True(rate is < 0.000001 and > (-0.000001));
	}
}
