using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using Mono.Options;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.CommandLine
{

	internal class PasswordFinderCommand : Command
	{
		public string Secret { get; set; }
		public string Language { get; set; }
		public bool UseNumbers { get; set; }
		public bool UseSymbols { get; set; }
		public bool ShowHelp { get; set; }

		public PasswordFinderCommand()
			: base("findpassword", "Try to find typos in provided password.")
		{
			Language = "en";

			Options = new OptionSet () {
				"usage: findpassword --secret:encrypted-secret --language:lang --numbers:[TRUE|FALSE] --symbold:[TRUE|FALSE]",
				"",
				"Tries to find typing mistakes in the user password by brute forcing it char by char.",
				"eg: findpassword --secret:6PYSeErf23ArQL7xXUWPKa3VBin6cuDaieSdABvVyTA51dS4Mxrtg1CpGN --numbers:false --symbold:true",
				"",
				{ "s|secret=", "The secret from your .json file (EncryptedSecret).",
					v => Secret = v },
				{ "l|language=", "The charset to use: en, es, it, fr, pt. Default=en.",
					v => Language = v },
				{ "n|numbers=", "Try passwords with numbers. Default=true.",
					v => UseNumbers = (v=="" || v=="1" || v.ToUpper()=="TRUE") },
				{ "x|symbols=", "Try passwords with symbolds. Default=true.",
					v => UseSymbols = (v=="" || v=="1" || v.ToUpper()=="TRUE") },
				{ "h|help", "Show Help",
					v => ShowHelp = true}};
		}

		public override int Invoke(IEnumerable<string> args)
		{
			var error = false;
			try
			{
				var extra = Options.Parse(args);
				if (ShowHelp)
				{
					Options.WriteOptionDescriptions(CommandSet.Out);
				}
				else if (string.IsNullOrEmpty(Secret))
				{
					Console.WriteLine("commands: Missing required argument `--secret=ENCRYPTED-SECRET`.");
					Console.WriteLine("commands: Use `findpassword --help` for details.");
					error = true;
				}
				else if (!PasswordFinder.Charsets.ContainsKey(Language))
				{
					Console.WriteLine($"commands: `{Language}` is not available language try with `en, es, pt, it or fr`.");
					Console.WriteLine("commands: Use `findpassword --help` for details.");
					error = true;
				}
				else
				{
					PasswordFinder.Find(Secret, Language, UseNumbers, UseSymbols);
				}
			}
			catch(Exception)
			{
				Console.WriteLine($"commands: There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return 0;
		}
	}

	internal class PasswordFinder
	{
		internal static Dictionary<string, string> Charsets = new Dictionary<string, string>{
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
			catch(FormatException)
			{
				Console.WriteLine("ERROR: The encrypted secret is invalid. Make sure you copied correctly from your wallet file.");
				return;
			}
			
			Console.WriteLine($"WARNING: This tool will display you password if it finds it. Also, the process status display your wong password chars.");
			Console.WriteLine($"         You can cancel this by CTRL+C combination anytime." + Environment.NewLine);

			Console.Write("Enter password: ");

			var password = PasswordConsole.ReadPassword();
			var charset = Charsets[language] + (useNumbers ? "0123456789" : "") + (useSymbols ? "|!¡@$¿?_-\"#$/%&()´+*=[]{},;:.^`<>" : "");

			var found = false;
			var lastpwd = string.Empty;
			var attempts = 0;
			var maxNumberAttempts = password.Length * charset.Length;
			var stepSize = (maxNumberAttempts + 101) / 100;


			Console.WriteLine();
			Console.Write($"[{string.Empty, 100}] 0%");

			var sw = new Stopwatch();
			sw.Start();
			foreach(var pwd in GeneratePasswords(password, charset.ToArray()))
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

			Console.WriteLine(Environment.NewLine);
			Console.WriteLine($"Completed in {sw.Elapsed}");
			Console.WriteLine(found ? $"SUCCESS: Password found: >>> {lastpwd} <<<" : "FAILED: Password not found");
			Console.WriteLine();
		}

		private static void Progress(int iter, int stepSize, int max, TimeSpan elapsed)
		{
			if(iter % stepSize == 0)
			{
				var percentage = (int)((float)iter / max * 100);
				var estimatedTime = elapsed / percentage * (100 - percentage);
				var bar = new string('#', percentage);

				Console.CursorLeft = 0;
				Console.Write($"[{bar, -100}] {percentage}% - ET: {estimatedTime}");
			}
		}

		private static IEnumerable<string> GeneratePasswords(string password, char[] charset)
		{
			var pwChar = password.ToCharArray();
			for(var i=0; i < pwChar.Length; i++)
			{
				var original = pwChar[i]; 
				foreach(var c in charset)
				{
					pwChar[i] = c;
					yield return new string(pwChar); 
				}
				pwChar[i] = original; 
			}
		}
	}
}
