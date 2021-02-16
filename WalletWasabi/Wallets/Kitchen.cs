using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Wallets
{
	public class Kitchen
	{
		private string Salt { get; set; } = null;
		private string Soup { get; set; } = null;
		private object RefrigeratorLock { get; } = new object();

		public bool HasIngredients => Salt is { } && Soup is { };

		public string SaltSoup()
		{
			if (!HasIngredients)
			{
				return "";
			}

			string res;
			lock (RefrigeratorLock)
			{
				res = StringCipher.Decrypt(Soup, Salt);
			}

			Cook(res);

			return res;
		}

		public void Cook(string ingredients)
		{
			lock (RefrigeratorLock)
			{
				ingredients ??= "";

				Salt = RandomString.AlphaNumeric(21, secureRandom: true);
				Soup = StringCipher.Encrypt(ingredients, Salt);
			}
		}
	}
}
