using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto
{
	public static class StringCipher
	{
		private const string DefaultPassphrase = "Satoshi";

		public static string Encrypt(string text, string passphrase)
		{
			passphrase = string.IsNullOrEmpty(passphrase) ? DefaultPassphrase : passphrase;
			byte[] hash = BitConverter.GetBytes(text.GetHashCode());
			byte[] valueBytes = Encoding.UTF8.GetBytes(text);
			byte[] passwordBytes = Encoding.UTF8.GetBytes(passphrase);
			var list = valueBytes.Select((b, i) => (byte)(b ^ passwordBytes[i % passwordBytes.Length])).ToArray();
			return Convert.ToBase64String(hash.Concat(list).ToArray());
		}

		public static string Decrypt(string cipherText, string passphrase)
		{
			passphrase = string.IsNullOrEmpty(passphrase) ? DefaultPassphrase : passphrase;
			var cipherTextBytes = Convert.FromBase64String(cipherText);
			var expectedHashBytes = cipherTextBytes.Take(sizeof(int));
			var textBytes = cipherTextBytes.Skip(sizeof(int));
			byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase);

			var list = textBytes.Select((b, i) => (byte)(b ^ passphraseBytes[i % passphraseBytes.Length])).ToArray();
			var result = Encoding.UTF8.GetString(list);
			byte[] hash = BitConverter.GetBytes(result.GetHashCode());
			if (!expectedHashBytes.SequenceEqual(hash))
			{
				throw new CryptographicException();
			}

			return result;
		}
	}
}
