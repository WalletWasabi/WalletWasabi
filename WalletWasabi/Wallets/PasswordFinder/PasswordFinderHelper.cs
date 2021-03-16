using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security;
using System.Threading;

namespace WalletWasabi.Wallets.PasswordFinder
{
	public static class PasswordFinderHelper
	{
		public static readonly Dictionary<Charset, string> Charsets = new()
		{
			[Charset.en] = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
			[Charset.es] = "aábcdeéfghiíjkmnñoópqrstuúüvwxyzAÁBCDEÉFGHIÍJKLMNNOÓPQRSTUÚÜVWXYZ",
			[Charset.pt] = "aáàâābcçdeéêfghiíjkmnoóôōpqrstuúvwxyzAÁÀÂĀBCÇDEÉÊFGHIÍJKMNOÓÔŌPQRSTUÚVWXYZ",
			[Charset.it] = "abcdefghimnopqrstuvxyzABCDEFGHILMNOPQRSTUVXYZ",
			[Charset.fr] = "aâàbcçdæeéèëœfghiîïjkmnoôpqrstuùüvwxyÿzAÂÀBCÇDÆEÉÈËŒFGHIÎÏJKMNOÔPQRSTUÙÜVWXYŸZ",
		};

		public static bool TryFind(PasswordFinderOptions passwordFinderOptions, [NotNullWhen(true)] out string? foundPassword, Action<int, TimeSpan>? reportPercentage = null, CancellationToken cancellationToken = default)
		{
			foundPassword = null;
			BitcoinEncryptedSecretNoEC encryptedSecret = passwordFinderOptions.Wallet.KeyManager.EncryptedSecret;

			var charset = Charsets[passwordFinderOptions.Charset] + (passwordFinderOptions.UseNumbers ? "0123456789" : "") + (passwordFinderOptions.UseSymbols ? "|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>" : "");

			var attempts = 0;
			var likelyPassword = passwordFinderOptions.LikelyPassword;
			var maxNumberAttempts = likelyPassword.Length * charset.Length;

			Stopwatch sw = Stopwatch.StartNew();

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
				var percentage = (double)attempts / maxNumberAttempts * 100;
				var remainingMilliseconds = sw.Elapsed.TotalMilliseconds / percentage * (100 - percentage);

				reportPercentage?.Invoke((int)percentage, TimeSpan.FromMilliseconds(remainingMilliseconds));
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
