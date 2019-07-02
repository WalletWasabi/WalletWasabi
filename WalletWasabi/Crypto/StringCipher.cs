using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.Crypto
{
	// https://stackoverflow.com/a/10177020/2061103
	public static class StringCipher
	{
		// This constant is used to determine the keysize of the encryption algorithm in bits.
		// We divide this by 8 within the code below to get the equivalent number of bytes.
		private const int Keysize = 128;

		// This constant determines the number of iterations for the password bytes generation function.
		private const int DerivationIterations = 1000;

		public static string Encrypt(string plainText, string passPhrase)
		{
			// Salt is randomly generated each time, but is preprended to encrypted cipher text
			// so that the same Salt value can be used when decrypting.
			byte[] salt = Generate128BitsOfRandomEntropy();
			byte[] iv = null;
			byte[] cipherTextBytes = null;
			byte[] key = null;
			var plainTextBytes = Encoding.UTF8.GetBytes(plainText);

			using (var password = new Rfc2898DeriveBytes(passPhrase, salt, DerivationIterations))
			{
				key = password.GetBytes(Keysize / 8);
				using (var aes = CreateAES())
				{
					aes.GenerateIV();
					iv = aes.IV;
					using (var encryptor = aes.CreateEncryptor(key, iv))
					{
						using (var memoryStream = new MemoryStream())
						{
							using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
							{
								cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
								cryptoStream.FlushFinalBlock();
								cryptoStream.Close();
							}
							cipherTextBytes = memoryStream.ToArray();
						}
					}
				}
			}

			using (var memoryStream = new MemoryStream())
			{
				using (var writer = new BinaryWriter(memoryStream))
				{
					writer.Write(salt);
					writer.Write(iv);
					using (var hmac = new HMACSHA256(key))
					{
						var authenticationCode = hmac.ComputeHash(iv.Concat(cipherTextBytes).ToArray());
						writer.Write(authenticationCode);
					}
					writer.Write(cipherTextBytes);
					writer.Flush();
				}

				var cipherTextWithAuthBytes = memoryStream.ToArray();
				memoryStream.Close();
				return Convert.ToBase64String(cipherTextWithAuthBytes);
			}
		}

		public static string Decrypt(string cipherText, string passPhrase)
		{
			var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
			byte[] key = null;
			byte[] iv = null;

			using (var memoryStream = new MemoryStream(cipherTextBytesWithSaltAndIv))
			{
				var cipherLength = 0;
				using (var reader = new BinaryReader(memoryStream, Encoding.UTF8, true))
				{
					var salt = reader.ReadBytes(Keysize / 8);
					iv = reader.ReadBytes(Keysize / 8);
					var authenticationCode = reader.ReadBytes(32);
					cipherLength = (int)(memoryStream.Length - memoryStream.Position);
					var cipher = reader.ReadBytes(cipherLength);

					using (var password = new Rfc2898DeriveBytes(passPhrase, salt, DerivationIterations))
					{
						key = password.GetBytes(Keysize / 8);
					}

					using (var hmac = new HMACSHA256(key))
					{
						var calculatedAuthenticationCode = hmac.ComputeHash(iv.Concat(cipher).ToArray());
						for (var i = 0; i < calculatedAuthenticationCode.Length; i++)
						{
							if (calculatedAuthenticationCode[i] != authenticationCode[i])
							{
								throw new CryptographicException("Message Authentication failed. Message has been modified or wrong password");
							}
						}
					}
				}

				using (var aes = CreateAES())
				{
					using (var decryptor = aes.CreateDecryptor(key, iv))
					{
						memoryStream.Seek(-cipherLength, SeekOrigin.End);
						using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
						{
							var plainTextBytes = new byte[cipherLength];
							var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
							memoryStream.Close();
							cryptoStream.Close();
							return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
						}
					}
				}
			}
		}

		private static AesManaged CreateAES()
		{
			var aes = new AesManaged
			{
				BlockSize = 128,
				Mode = CipherMode.CBC,
				Padding = PaddingMode.PKCS7
			};
			return aes;
		}

		private static byte[] Generate128BitsOfRandomEntropy()
		{
			var randomBytes = new byte[16]; // 16 Bytes will give us 128 bits.
			using (var rngCsp = new RNGCryptoServiceProvider())
			{
				// Fill the array with cryptographically secure random bytes.
				rngCsp.GetBytes(randomBytes);
			}
			return randomBytes;
		}
	}
}
