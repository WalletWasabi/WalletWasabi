using System;
using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class HashHelpers
	{
		public static string GenerateSha256Hash(string input)
		{
			using var sha256 = SHA256.Create();
			var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

			return ByteHelpers.ToHex(hash);
		}

		public static byte[] GenerateSha256Hash(byte[] input)
		{
			using var sha256 = SHA256.Create();
			var hash = sha256.ComputeHash(input);

			return hash;
		}

		public static int ComputeHashCode(params byte[] data)
		{
			var hash = new HashCode();
			foreach (var element in data)
			{
				hash.Add(element);
			}
			return hash.ToHashCode();
		}
	}
}
