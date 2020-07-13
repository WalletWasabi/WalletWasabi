using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness
{
	public class Characters
	{
		public readonly static Characters AlphaNumeric = new Characters("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
		public readonly static Characters CapitalAlphaNumeric = new Characters("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

		public Characters(string chars)
		{
			Guard.NotNullOrEmpty(nameof(chars), chars);
			Chars = chars;
		}

		public string Chars { get; }
	}
}
