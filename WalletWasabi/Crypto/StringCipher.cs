using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.Crypto
{
	/// <remarks>Based on https://tomrucki.com/posts/aes-encryption-in-csharp/.</remarks>
	public static class StringCipher
	{
		private const int AesBlockByteSize = 128 / 8;

		private const int PasswordSaltByteSize = 128 / 8;
		private const int PasswordByteSize = 256 / 8;
		private const int PasswordIterationCount = 100_000;

		private const int SignatureByteSize = 256 / 8;

		private const int MinimumEncryptedMessageByteSize =
			PasswordSaltByteSize + // Auth salt
			PasswordSaltByteSize + // Key salt
			AesBlockByteSize + // IV
			AesBlockByteSize + // Cipher text min length
			SignatureByteSize; // Signature tag

		private static readonly Encoding StringEncoding = Encoding.UTF8;
		private static readonly RandomNumberGenerator Random = RandomNumberGenerator.Create();

		public static string Encrypt(string toEncrypt, string password)
		{
			byte[] keySalt = GenerateRandomBytes(PasswordSaltByteSize);
			byte[] key = GetKey(password, keySalt);
			byte[] iv = GenerateRandomBytes(AesBlockByteSize);

			byte[] cipherText;
			using (Aes aes = CreateAes())
			using (ICryptoTransform encryptor = aes.CreateEncryptor(key, iv))
			{
				byte[] plainText = StringEncoding.GetBytes(toEncrypt);
				cipherText = encryptor.TransformFinalBlock(plainText, 0, plainText.Length);
			}

			// Sign.
			byte[] authKeySalt = GenerateRandomBytes(PasswordSaltByteSize);
			byte[] authKey = GetKey(password, authKeySalt);
			byte[] result = MergeArrays(additionalCapacity: SignatureByteSize, authKeySalt, keySalt, iv, cipherText);

			using (HMACSHA256 hmac = new(authKey))
			{
				int payloadToSignLength = result.Length - SignatureByteSize;
				byte[] signatureTag = hmac.ComputeHash(result, 0, payloadToSignLength);
				signatureTag.CopyTo(result, payloadToSignLength);
			}

			return Convert.ToBase64String(result);
		}

		public static string Decrypt(string encryptedString, string password)
		{
			byte[] encryptedData = Convert.FromBase64String(encryptedString);

			if (encryptedData is null || encryptedData.Length < MinimumEncryptedMessageByteSize)
			{
				throw new ArgumentException("Invalid length of encrypted data");
			}

			byte[] authKeySalt = encryptedData.AsSpan(0, PasswordSaltByteSize).ToArray();
			byte[] keySalt = encryptedData.AsSpan(PasswordSaltByteSize, PasswordSaltByteSize).ToArray();
			byte[] iv = encryptedData.AsSpan(2 * PasswordSaltByteSize, AesBlockByteSize).ToArray();
			byte[] signatureTag = encryptedData.AsSpan(encryptedData.Length - SignatureByteSize, SignatureByteSize).ToArray();

			int cipherTextIndex = authKeySalt.Length + keySalt.Length + iv.Length;
			int cipherTextLength = encryptedData.Length - cipherTextIndex - signatureTag.Length;

			byte[] authKey = GetKey(password, authKeySalt);
			byte[] key = GetKey(password, keySalt);

			// Verify signature.
			using (HMACSHA256 hmac = new(authKey))
			{
				int payloadToSignLength = encryptedData.Length - SignatureByteSize;
				byte[] signatureTagExpected = hmac.ComputeHash(encryptedData, 0, payloadToSignLength);

				// Constant time checking to prevent timing attacks.
				int signatureVerificationResult = 0;

				for (int i = 0; i < signatureTag.Length; i++)
				{
					signatureVerificationResult |= signatureTag[i] ^ signatureTagExpected[i];
				}

				if (signatureVerificationResult != 0)
				{
					throw new CryptographicException("Invalid signature");
				}
			}

			// Decrypt.
			using Aes aes = CreateAes();
			using ICryptoTransform encryptor = aes.CreateDecryptor(key, iv);
			byte[] decryptedBytes = encryptor
				.TransformFinalBlock(encryptedData, cipherTextIndex, cipherTextLength);
			return StringEncoding.GetString(decryptedBytes);
		}

		private static Aes CreateAes()
		{
			Aes aes = Aes.Create();
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			return aes;
		}

		private static byte[] GetKey(string password, byte[] passwordSalt)
		{
			byte[] keyBytes = StringEncoding.GetBytes(password);
			using Rfc2898DeriveBytes derivator = new( keyBytes, passwordSalt, PasswordIterationCount, HashAlgorithmName.SHA256);
			return derivator.GetBytes(PasswordByteSize);
		}

		private static byte[] GenerateRandomBytes(int numberOfBytes)
		{
			byte[] randomBytes = new byte[numberOfBytes];
			Random.GetBytes(randomBytes);
			return randomBytes;
		}

		private static byte[] MergeArrays(int additionalCapacity = 0, params byte[][] arrays)
		{
			byte[] merged = new byte[arrays.Sum(a => a.Length) + additionalCapacity];
			int mergeIndex = 0;

			for (int i = 0; i < arrays.GetLength(0); i++)
			{
				arrays[i].CopyTo(merged, mergeIndex);
				mergeIndex += arrays[i].Length;
			}

			return merged;
		}
	}
}
