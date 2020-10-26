using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.CommandLine
{
	public class PasswordFinderCommand : Command
	{
		public PasswordFinderCommand(WalletManager walletManager)
			: base("findpassword", "Try to find typos in provided password.")
		{
			WalletManager = Guard.NotNull(nameof(walletManager), walletManager);
			Language = "en";

			Options = new OptionSet()
			{
				"usage: findpassword --wallet:WalletName --language:lang --numbers:[TRUE|FALSE] --symbols:[TRUE|FALSE]",
				"",
				"Tries to find typing mistakes in the user password by brute forcing it char by char.",
				"eg: ./wassabee findpassword --wallet:MyWalletName --numbers:false --symbols:true",
				"",
				{ "w|wallet=", "The name of the wallet file.", x => WalletName = x },
				{ "s|secret=", "You can specify an encrypted secret key instead of wallet. Example of encrypted secret: 6PYTMDmkxQrSv8TK4761tuKrV8yFwPyZDqjJafcGEiLBHiqBV6WviFxJV4", x => EncryptedSecret = Guard.Correct(x) },
				{ "l|language=", "The charset to use: en, es, it, fr, pt. Default=en.", v => Language = v },
				{ "n|numbers=", "Try passwords with numbers. Default=false.", v => UseNumbers = (v.Length == 0 || v == "1" || v.ToUpper() == "TRUE") },
				{ "x|symbols=", "Try passwords with symbols. Default=false.", v => UseSymbols = (v.Length == 0 || v == "1" || v.ToUpper() == "TRUE") },
				{ "h|help", "Show Help", v => ShowHelp = true }
			};
		}

		public string? WalletName { get; set; }
		public string? EncryptedSecret { get; set; }
		public WalletManager WalletManager { get; }
		public string Language { get; set; }
		public bool UseNumbers { get; set; }
		public bool UseSymbols { get; set; }
		public bool ShowHelp { get; set; }

		public override Task<int> InvokeAsync(IEnumerable<string> args)
		{
			try
			{
				Options.Parse(args);
				if (ShowHelp)
				{
					Options.WriteOptionDescriptions(CommandSet.Out);
				}
				else if (string.IsNullOrWhiteSpace(WalletName) && string.IsNullOrWhiteSpace(EncryptedSecret))
				{
					throw new InvalidOperationException("Missing required argument `--wallet=WalletName`. Use `findpassword --help` for details.");
				}
				else if (!PasswordFinder.Charsets.ContainsKey(Language))
				{
					throw new InvalidOperationException($"`{Language}` is not available language, try with `en, es, pt, it or fr`. Use `findpassword --help` for details.");
				}
				else if (!string.IsNullOrWhiteSpace(WalletName))
				{
					KeyManager km = WalletManager.GetWalletByName(WalletName).KeyManager;
					PasswordFinder.Find(km.EncryptedSecret.ToWif(), Language, UseNumbers, UseSymbols);
				}
				else if (!string.IsNullOrWhiteSpace(EncryptedSecret))
				{
					PasswordFinder.Find(EncryptedSecret, Language, UseNumbers, UseSymbols);
				}
			}
			catch (Exception ex)
			{
				return Task.FromException<int>(ex);
			}
			return Task.FromResult(0);
		}
	}
}
