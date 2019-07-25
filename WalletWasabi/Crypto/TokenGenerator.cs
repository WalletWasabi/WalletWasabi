using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.Crypto
{
	// https://gist.github.com/diegojancic/9f78750f05550fa6039d2f6092e461e5
	// Based on Eric J's answer here: https://stackoverflow.com/a/1344255/72350
	//  - Fixed bias by using 64 characters
	//  - Removed unneeded 'new char[62]' and 'new byte[1]'
	//  - Using GetBytes instead of GetNonZeroBytes
	public static class TokenGenerator
	{
		public static string GetUniqueKey(int length)
		{
			char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890.-".ToCharArray();
			byte[] data = new byte[length];
			using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
			{
				crypto.GetBytes(data);
			}
			StringBuilder result = new StringBuilder(length);
			foreach (byte b in data)
			{
				result.Append(chars[b % chars.Length]);
			}
			return result.ToString();
		}
	}
}
