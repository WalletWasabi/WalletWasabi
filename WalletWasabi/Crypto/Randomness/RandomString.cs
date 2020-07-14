using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness
{
	public static class RandomString
	{
		public static string FromCharacters(int length, string characters, bool secureRandom = false)
		{
			IWasabiRandom random;
			if (secureRandom)
			{
				using var rand = new SecureRandom();
				random = rand;
			}
			else
			{
				random = new InsecureRandom();
			}

			return random.GetString(length, characters);
		}

		public static string AlphaNumeric(int length, bool secureRandom = false) => FromCharacters(length, Constants.AlphaNumericCharacters, secureRandom);

		public static string CapitalAlphaNumeric(int length, bool secureRandom = false) => FromCharacters(length, Constants.CapitalAlphaNumericCharacters, secureRandom);
	}
}
