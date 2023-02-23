using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto;

public static class StringCipher
{
	// This code is strongly inspired by https://tomrucki.com/posts/aes-encryption-in-csharp/ and
	// https://netnix.org/2015/04/19/aes-encryption-with-hmac-integrity-in-java/
	private const int KeyByteSize = 128 / 8;

	private const int AesBlockByteSize = 128 / 8;
	private const int PasswordSaltByteSize = 128 / 8;
	private const int SignatureByteSize = 256 / 8;

	// This constant determines the number of iterations for the password bytes generation function.
	private const int DerivationIterations = 1000;

	private const int MinimumEncryptedMessageByteSize =
		PasswordSaltByteSize + // Auth salt
		PasswordSaltByteSize + // Key salt
		AesBlockByteSize + // IV
		AesBlockByteSize + // Cipher text min length
		SignatureByteSize; // Signature tag

	public static string Encrypt(string plainText, string passPhrase)
	{
		// Salt is randomly generated each time, but is prepended to encrypted cipher text
		// so that the same Salt value can be used when decrypting.
		byte[] iv = SecureRandom.Instance.GetBytes(AesBlockByteSize);
		byte[] encryptionKeySalt = SecureRandom.Instance.GetBytes(PasswordSaltByteSize);
		byte[] encryptionKey = DerivateKey(passPhrase, encryptionKeySalt);

		// Encrypt the plain text.
		using var aes = CreateAES(encryptionKey);
		byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
		byte[] cipherTextBytes = aes.EncryptCbc(plainTextBytes, iv);

		// Authenticate.
		byte[] authKeySalt = SecureRandom.Instance.GetBytes(PasswordSaltByteSize);
		byte[] authKey = DerivateKey(passPhrase, authKeySalt);
		byte[] result = MergeArrays(additionalCapacity: SignatureByteSize, encryptionKeySalt, iv, authKeySalt, cipherTextBytes);

		byte[] authCode = HMACSHA256.HashData(authKey, result[..^SignatureByteSize]);
		authCode.CopyTo(result, result.Length - SignatureByteSize);

		return Convert.ToBase64String(result);
	}

	public static string Decrypt(string encryptedString, string passPhrase)
	{
		byte[] encryptedData = Convert.FromBase64String(encryptedString);

		if (encryptedData is null || encryptedData.Length < MinimumEncryptedMessageByteSize)
		{
			throw new ArgumentException("Invalid length of encrypted data.");
		}

		Span<byte> encryptionKeySalt = encryptedData.AsSpan(0, PasswordSaltByteSize);
		Span<byte> iv = encryptedData.AsSpan(PasswordSaltByteSize, AesBlockByteSize);
		Span<byte> authKeySalt = encryptedData.AsSpan(PasswordSaltByteSize + AesBlockByteSize, PasswordSaltByteSize);
		Span<byte> authCode = encryptedData.AsSpan(encryptedData.Length - SignatureByteSize, SignatureByteSize);

		// Authenticate.
		byte[] authKey = DerivateKey(passPhrase, authKeySalt);

		byte[] expectedAuthCode = HMACSHA256.HashData(authKey, encryptedData[..^SignatureByteSize]);

		if (!authCode.SequenceEqual(expectedAuthCode))
		{
			throw new CryptographicException("Message Authentication failed. Message has been modified or wrong password.");
		}

		// Decrypt.
		int cipherTextIndex = authKeySalt.Length + encryptionKeySalt.Length + iv.Length;
		int cipherTextLength = encryptedData.Length - cipherTextIndex - authCode.Length;

		byte[] encryptionKey = DerivateKey(passPhrase, encryptionKeySalt);
		using var aes = CreateAES(encryptionKey);
		byte[] plainTextBytes = aes.DecryptCbc(encryptedData.AsSpan(cipherTextIndex, cipherTextLength), iv);

		return Encoding.UTF8.GetString(plainTextBytes);
	}

	private static byte[] DerivateKey(string passPhrase, Span<byte> salt) =>
		Rfc2898DeriveBytes.Pbkdf2(passPhrase, salt, DerivationIterations, HashAlgorithmName.SHA256, KeyByteSize);

	private static Aes CreateAES(byte[] encryptionKey)
	{
		Aes aes = Aes.Create();
		aes.Key = encryptionKey;
		return aes;
	}

	private static byte[] MergeArrays(int additionalCapacity, params byte[][] arrays)
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
