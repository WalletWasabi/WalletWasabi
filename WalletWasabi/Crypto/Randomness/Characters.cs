using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness
{
	public class Characters
	{
		public static readonly Characters AlphaNumeric = new Characters("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
		public static readonly Characters CapitalAlphaNumeric = new Characters("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

		public Characters(string chars)
		{
			Guard.NotNullOrEmpty(nameof(chars), chars);
			Chars = chars;
		}

		public string Chars { get; }
	}
}
