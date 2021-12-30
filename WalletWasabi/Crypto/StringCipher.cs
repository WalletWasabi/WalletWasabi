using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto
{
	// https://stackoverflow.com/a/10177020/2061103
	public static class StringCipher
	{
		// This constant is used to determine the keysize of the encryption algorithm in bits.
		// We divide this by 8 within the code below to get the equivalent number of bytes.
		private const int KeySize = 128;

		// This constant determines the number of iterations for the password bytes generation function.
		private const int DerivationIterations = 1000;

		public static string Encrypt(string plainText, string passPhrase)
		{
			// Salt is randomly generated each time, but is prepended to encrypted cipher text
			// so that the same Salt value can be used when decrypting.
			byte[] salt = Generate128BitsOfRandomEntropy();
			byte[] cipherTextBytes;
			var plainTextBytes = Encoding.UTF8.GetBytes(plainText);

			byte[] key = DerivateKey(passPhrase, salt);
			using var aes = CreateAES();
			aes.GenerateIV();
			byte[] iv = aes.IV;
			using var encryptor = aes.CreateEncryptor(key, iv);
			using var memoryStreamEncryptor = new MemoryStream();
			using (var cryptoStream = new CryptoStream(memoryStreamEncryptor, encryptor, CryptoStreamMode.Write))
			{
				cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
				cryptoStream.FlushFinalBlock();
			}
			cipherTextBytes = memoryStreamEncryptor.ToArray();

			using var memoryStream = new MemoryStream();
			using var writer = new BinaryWriter(memoryStream);
			writer.Write(salt);
			writer.Write(iv);
			using var hmac = new HMACSHA256(key);
			var authenticationCode = hmac.ComputeHash(iv.Concat(cipherTextBytes).ToArray());
			writer.Write(authenticationCode);
			writer.Write(cipherTextBytes);
			writer.Flush();

			var cipherTextWithAuthBytes = memoryStream.ToArray();
			return Convert.ToBase64String(cipherTextWithAuthBytes);
		}

		public static string Decrypt(string cipherText, string passPhrase)
		{
			var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
			byte[] key;
			byte[] iv;

			using var memoryStream = new MemoryStream(cipherTextBytesWithSaltAndIv);
			int cipherLength;
			int cipherStartIndex;
			using (var reader = new BinaryReader(memoryStream, Encoding.UTF8, true))
			{
				var salt = reader.ReadBytes(KeySize / 8);
				iv = reader.ReadBytes(KeySize / 8);
				var authenticationCode = reader.ReadBytes(32);
				cipherStartIndex = (int)memoryStream.Position;
				cipherLength = (int)(memoryStream.Length - memoryStream.Position);
				var cipher = reader.ReadBytes(cipherLength);
				key = DerivateKey(passPhrase, salt);

				using var hmac = new HMACSHA256(key);
				var calculatedAuthenticationCode = hmac.ComputeHash(iv.Concat(cipher).ToArray());
				for (var i = 0; i < calculatedAuthenticationCode.Length; i++)
				{
					if (calculatedAuthenticationCode[i] != authenticationCode[i])
					{
						throw new CryptographicException("Message Authentication failed. Message has been modified or wrong password");
					}
				}
			}

			using var aes = CreateAES();
			using var decryptor = aes.CreateDecryptor(key, iv);
			byte[] plainTextBytes = decryptor.TransformFinalBlock(cipherTextBytesWithSaltAndIv, cipherStartIndex, cipherLength);

			return Encoding.UTF8.GetString(plainTextBytes);
		}

		private static byte[] DerivateKey(string passPhrase, byte[] salt)
		{
			using var password = new Rfc2898DeriveBytes(passPhrase, salt, DerivationIterations);
			return password.GetBytes(KeySize / 8);
		}

		private static Aes CreateAES()
		{
			Aes aes = Aes.Create();
			aes.BlockSize = 128;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;

			return aes;
		}

		private static byte[] Generate128BitsOfRandomEntropy()
		{
			using var secureRandom = new SecureRandom();
			var randomBytes = secureRandom.GetBytes(16); // 16 Bytes will give us 128 bits.
			return randomBytes;
		}
	}
}
