using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Wallets
{
	public class Kitchen
	{
		private string? Salt { get; set; } = null;
		private string? Soup { get; set; } = null;
		private object RefrigeratorLock { get; } = new object();

		[MemberNotNullWhen(returnValue: true, nameof(Salt), nameof(Soup))]
		public bool HasIngredients => Salt is not null && Soup is not null;

		public string SaltSoup()
		{
			if (!HasIngredients)
			{
				throw new InvalidOperationException("Ingredients are missing.");
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

		public void CleanUp()
		{
			lock (RefrigeratorLock)
			{
				Salt = null;
				Soup = null;
			}
		}
	}
}
