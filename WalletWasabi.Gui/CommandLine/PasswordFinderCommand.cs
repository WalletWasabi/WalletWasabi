using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.CommandLine
{
	internal class PasswordFinderCommand : Command
	{
		public string WalletName { get; set; }
		public string EncryptedSecret { get; set; }
		public Daemon Daemon { get; }
		public string Language { get; set; }
		public bool UseNumbers { get; set; }
		public bool UseSymbols { get; set; }
		public bool ShowHelp { get; set; }

		public PasswordFinderCommand(Daemon daemon)
			: base("findpassword", "Try to find typos in provided password.")
		{
			Daemon = daemon;
			Language = "en";

			Options = new OptionSet() {
				"usage: findpassword --wallet:WalletName --language:lang --numbers:[TRUE|FALSE] --symbold:[TRUE|FALSE]",
				"",
				"Tries to find typing mistakes in the user password by brute forcing it char by char.",
				"eg: ./wassabee findpassword --wallet:MyWalletName --numbers:false --symbold:true",
				"",
				{ "w|wallet=", "The name of the wallet file.",
					x =>  WalletName = x },
				{ "s|secret=", "You can specify an encrypted secret key instead of wallet. Example of encrypted secret: 6PYTMDmkxQrSv8TK4761tuKrV8yFwPyZDqjJafcGEiLBHiqBV6WviFxJV4",
					x =>  EncryptedSecret = Guard.Correct(x) },
				{ "l|language=", "The charset to use: en, es, it, fr, pt. Default=en.",
					v => Language = v },
				{ "n|numbers=", "Try passwords with numbers. Default=true.",
					v => UseNumbers = (v=="" || v=="1" || v.ToUpper()=="TRUE") },
				{ "x|symbols=", "Try passwords with symbolds. Default=true.",
					v => UseSymbols = (v=="" || v=="1" || v.ToUpper()=="TRUE") },
				{ "h|help", "Show Help",
					v => ShowHelp = true}};
		}

		public override Task<int> InvokeAsync(IEnumerable<string> args)
		{
			var error = false;
			try
			{
				var extra = Options.Parse(args);
				if (ShowHelp)
				{
					Options.WriteOptionDescriptions(CommandSet.Out);
				}
				else if (string.IsNullOrWhiteSpace(WalletName) && string.IsNullOrWhiteSpace(EncryptedSecret))
				{
					Logging.Logger.LogCritical<PasswordFinderCommand>("Missing required argument `--wallet=WalletName`.");
					Logging.Logger.LogCritical<PasswordFinderCommand>("Use `findpassword --help` for details.");
					error = true;
				}
				else if (!PasswordFinder.Charsets.ContainsKey(Language))
				{
					Logging.Logger.LogCritical<PasswordFinderCommand>($"`{Language}` is not available language try with `en, es, pt, it or fr`.");
					Logging.Logger.LogCritical<PasswordFinderCommand>("Use `findpassword --help` for details.");
					error = true;
				}
				else if (!string.IsNullOrWhiteSpace(WalletName))
				{
					KeyManager km = Daemon.TryGetKeyManagerFromWalletName(WalletName);
					if (km is null)
					{
						error = true;
					}
					PasswordFinder.Find(km.EncryptedSecret.ToWif(), Language, UseNumbers, UseSymbols);
				}
				else if (!string.IsNullOrWhiteSpace(EncryptedSecret))
				{
					PasswordFinder.Find(EncryptedSecret, Language, UseNumbers, UseSymbols);
				}
			}
			catch (Exception)
			{
				Logging.Logger.LogCritical<PasswordFinderCommand>($"There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return Task.FromResult(0);
		}
	}
}
