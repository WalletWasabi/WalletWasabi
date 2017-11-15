using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HiddenWallet.Helpers
{
	public static class HashHelpers
    {
		/// <summary>
		/// quickly generates a short, relatively unique hash
		/// https://codereview.stackexchange.com/questions/102251/short-hash-generator
		/// </summary>
		public static string GenerateShortSha1Hash(string input)
		{
			using (SHA1Managed sha1 = new SHA1Managed())
			{
				var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));

				//make sure the hash is only alpha numeric to prevent charecters that may break the url
				return string.Concat(Convert.ToBase64String(hash).ToCharArray().Where(x => char.IsLetterOrDigit(x)).Take(10));
			}
		}
	}
}
