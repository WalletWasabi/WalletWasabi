using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NBitcoin.DataEncoders;

namespace MagicalCryptoWallet.Crypto
{
	// https://stackoverflow.com/a/10177020/2061103
	public static class StringCipher
	{
		// This constant is used to determine the keysize of the encryption algorithm in bits.
		// We divide this by 8 within the code below to get the equivalent number of bytes.
		private const int Keysize = 128;

		// This constant determines the number of iterations for the password bytes generation function.
		private const int DerivationIterations = 1000;

		private static AesManaged CreateAES()
		{
			var aes = new AesManaged();
			aes.BlockSize = 128;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			return aes;
		}

		public static string Encrypt(string plainText, string passPhrase)
		{
			// Salt is randomly generated each time, but is preprended to encrypted cipher text
			// so that the same Salt value can be used when decrypting.  
			var saltStringBytes = Generate128BitsOfRandomEntropy();

			var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
			using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
			{
				var keyBytes = password.GetBytes(Keysize / 8);
				using (var aes = CreateAES())
				{
					aes.GenerateIV();
					using (var encryptor = aes.CreateEncryptor(keyBytes, aes.IV))
					{
						using (var memoryStream = new MemoryStream())
						{
							memoryStream.Write(saltStringBytes, 0, saltStringBytes.Length);
							memoryStream.Write(aes.IV, 0, aes.IV.Length);
							using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
							{
								cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
								cryptoStream.FlushFinalBlock();
								cryptoStream.Close();
							}
							var cipherTextBytes = memoryStream.ToArray();
							memoryStream.Close();
							return Convert.ToBase64String(cipherTextBytes);
						}
					}
				}
			}
		}

		public static string Decrypt(string cipherText, string passPhrase)
		{
			var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);

			using (var memoryStream = new MemoryStream(cipherTextBytesWithSaltAndIv))
			{
				using (var reader = new BinaryReader(memoryStream))
				{
					var saltStringBytes = reader.ReadBytes(Keysize / 8);

					using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
					{
						var keyBytes = password.GetBytes(Keysize / 8);
						using (var aes = CreateAES())
						{
							var ivStringBytes = reader.ReadBytes(Keysize / 8);
							using (var decryptor = aes.CreateDecryptor(keyBytes, ivStringBytes))
							{
								using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
								{
									var plainTextBytes = new byte[memoryStream.Length - memoryStream.Position];
									var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
									memoryStream.Close();
									cryptoStream.Close();
									return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
								}
							}
						}
					}
				}
			}
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
