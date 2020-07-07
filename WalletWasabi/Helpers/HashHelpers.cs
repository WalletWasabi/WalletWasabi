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

		/// <summary>
		/// https://stackoverflow.com/a/468084/2061103
		/// </summary>
		public static int ComputeHashCode(params byte[] data)
		{
			unchecked
			{
				const int P = 16777619;
				int hash = (int)2166136261;

				for (int i = 0; i < data.Length; i++)
				{
					hash = (hash ^ data[i]) * P;
				}

				hash += hash << 13;
				hash ^= hash >> 7;
				hash += hash << 3;
				hash ^= hash >> 17;
				hash += hash << 5;
				return hash;
			}
		}
	}
}
