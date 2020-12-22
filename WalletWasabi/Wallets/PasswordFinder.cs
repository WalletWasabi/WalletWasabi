using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;

namespace WalletWasabi.Wallets
{
	public class PasswordFinder
	{
		public static readonly Dictionary<string, string> Charsets = new Dictionary<string, string>
		{
			["en"] = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
			["es"] = "aábcdeéfghiíjkmnñoópqrstuúüvwxyzAÁBCDEÉFGHIÍJKLMNNOÓPQRSTUÚÜVWXYZ",
			["pt"] = "aáàâābcçdeéêfghiíjkmnoóôōpqrstuúvwxyzAÁÀÂĀBCÇDEÉÊFGHIÍJKMNOÓÔŌPQRSTUÚVWXYZ",
			["it"] = "abcdefghimnopqrstuvxyzABCDEFGHILMNOPQRSTUVXYZ",
			["fr"] = "aâàbcçdæeéèëœfghiîïjkmnoôpqrstuùüvwxyÿzAÂÀBCÇDÆEÉÈËŒFGHIÎÏJKMNOÔPQRSTUÙÜVWXYŸZ",
		};

		public static bool TryFind(Wallet wallet, string language, bool useNumbers, bool useSymbols, string likelyPassword, Action<int> reportPercentage, out string? foundPassword, CancellationToken cancellationToken = default)
		{
			foundPassword = null;
			BitcoinEncryptedSecretNoEC encryptedSecret = wallet.KeyManager.EncryptedSecret;

			var charset = Charsets[language] + (useNumbers ? "0123456789" : "") + (useSymbols ? "|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>" : "");

			var attempts = 0;
			var maxNumberAttempts = likelyPassword.Length * charset.Length;

			foreach (var pwd in GeneratePasswords(likelyPassword, charset.ToArray()))
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					encryptedSecret.GetKey(pwd);
					foundPassword = pwd;
					return true;
				}
				catch (SecurityException)
				{
				}

				attempts++;
				var percentage = (int)((float)attempts / maxNumberAttempts * 100);

				reportPercentage.Invoke(percentage);
			}

			return false;
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
