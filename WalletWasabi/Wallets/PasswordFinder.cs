using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;

namespace WalletWasabi.Wallets
{
	public static class PasswordFinder
	{
		public static readonly Dictionary<string, string> Charsets = new Dictionary<string, string>
		{
			["en"] = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
			["es"] = "aábcdeéfghiíjkmnñoópqrstuúüvwxyzAÁBCDEÉFGHIÍJKLMNNOÓPQRSTUÚÜVWXYZ",
			["pt"] = "aáàâābcçdeéêfghiíjkmnoóôōpqrstuúvwxyzAÁÀÂĀBCÇDEÉÊFGHIÍJKMNOÓÔŌPQRSTUÚVWXYZ",
			["it"] = "abcdefghimnopqrstuvxyzABCDEFGHILMNOPQRSTUVXYZ",
			["fr"] = "aâàbcçdæeéèëœfghiîïjkmnoôpqrstuùüvwxyÿzAÂÀBCÇDÆEÉÈËŒFGHIÎÏJKMNOÔPQRSTUÙÜVWXYŸZ",
		};

		public static string? Find(Wallet wallet, string language, bool useNumbers, bool useSymbols, string likelyPassword, Action<int> reportPercentage)
		{
			BitcoinEncryptedSecretNoEC encryptedSecret = wallet.KeyManager.EncryptedSecret;

			var charset = Charsets[language] + (useNumbers ? "0123456789" : "") + (useSymbols ? "|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>" : "");

			var found = false;
			var lastpwd = "";
			var attempts = 0;
			var maxNumberAttempts = likelyPassword.Length * charset.Length;

			foreach (var pwd in GeneratePasswords(likelyPassword, charset.ToArray()))
			{
				lastpwd = pwd;
				try
				{
					encryptedSecret.GetKey(pwd);
					found = true;
					break;
				}
				catch (SecurityException)
				{
				}
				var percentage = (int)((float)++attempts / maxNumberAttempts * 100);

				reportPercentage.Invoke(percentage);
			}

			return found ? lastpwd : null;
		}

		private static IEnumerable<string> GeneratePasswords(string password, char[] charset)
		{
			var pwChar = password.ToCharArray();
			for (var i = 0; i < pwChar.Length; i++)
			{
				var original = pwChar[i];
				foreach (var c in charset)
				{
					pwChar[i] = c;
					yield return new string(pwChar);
				}
				pwChar[i] = original;
			}
		}
	}
}
