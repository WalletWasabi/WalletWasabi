using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.CommandLine
{
	internal class PasswordFinder
	{
		internal static Dictionary<string, string> Charsets = new Dictionary<string, string>
		{
			["en"] = "abcdefghijkmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
			["es"] = "aábcdeéfghiíjkmnñoópqrstuúüvwxyzAÁBCDEÉFGHIÍJKLMNNOÓPQRSTUÚÜVWXYZ",
			["pt"] = "aáàâābcçdeéêfghiíjkmnoóôōpqrstuúvwxyzAÁÀÂĀBCÇDEÉÊFGHIÍJKMNOÓÔŌPQRSTUÚVWXYZ",
			["it"] = "abcdefghimnopqrstuvxyzABCDEFGHILMNOPQRSTUVXYZ",
			["fr"] = "aâàbcçdæeéèëœfghiîïjkmnoôpqrstuùüvwxyÿzAÂÀBCÇDÆEÉÈËŒFGHIÎÏJKMNOÔPQRSTUÙÜVWXYŸZ",
		};

		internal static void Find(string secret, string language, bool useNumbers, bool useSymbols)
		{
			BitcoinEncryptedSecretNoEC encryptedSecret;
			try
			{
				encryptedSecret = new BitcoinEncryptedSecretNoEC(secret);
			}
			catch (FormatException)
			{
				Logging.Logger.LogCritical("ERROR: The encrypted secret is invalid. Make sure you copied correctly from your wallet file.");
				return;
			}

			Logging.Logger.LogWarning($"WARNING: This tool will display your password if it finds it.");
			Logging.Logger.LogWarning($"         You can cancel this by CTRL+C combination anytime.{Environment.NewLine}");

			Console.Write("Enter a likely password: ");

			var password = PasswordConsole.ReadPassword();
			var charset = Charsets[language] + (useNumbers ? "0123456789" : "") + (useSymbols ? "|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>" : "");

			var found = false;
			var lastpwd = string.Empty;
			var attempts = 0;
			var maxNumberAttempts = password.Length * charset.Length;
			var stepSize = (maxNumberAttempts + 101) / 100;

			Console.WriteLine();
			Console.Write($"[{string.Empty,100}] 0%");

			var sw = new Stopwatch();
			sw.Start();
			foreach (var pwd in GeneratePasswords(password, charset.ToArray()))
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
				Progress(++attempts, stepSize, maxNumberAttempts, sw.Elapsed);
			}
			sw.Stop();

			Console.WriteLine();
			Console.WriteLine();
			Logging.Logger.LogInfo($"Completed in {sw.Elapsed}");
			Console.WriteLine(found ? $"SUCCESS: Password found: >>> {lastpwd} <<<" : "FAILED: Password not found");
			Console.WriteLine();
		}

		private static void Progress(int iter, int stepSize, int max, TimeSpan elapsed)
		{
			if (iter % stepSize == 0)
			{
				var percentage = (int)((float)iter / max * 100);
				var estimatedTime = elapsed / percentage * (100 - percentage);
				var bar = new string('#', percentage);

				Console.CursorLeft = 0;
				Console.Write($"[{bar,-100}] {percentage}% - ET: {estimatedTime}");
			}
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
