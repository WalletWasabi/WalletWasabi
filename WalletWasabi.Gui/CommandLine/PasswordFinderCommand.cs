using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Gui.CommandLine
{
	internal class PasswordFinderCommand : Command
	{
		public string Wallet { get; set; }
		public string Language { get; set; }
		public bool UseNumbers { get; set; }
		public bool UseSymbols { get; set; }
		public bool ShowHelp { get; set; }

		public PasswordFinderCommand()
			: base("findpassword", "Try to find typos in provided password.")
		{
			Language = "en";

			Options = new OptionSet() {
				"usage: findpassword --secret:encrypted-secret --language:lang --numbers:[TRUE|FALSE] --symbold:[TRUE|FALSE]",
				"",
				"Tries to find typing mistakes in the user password by brute forcing it char by char.",
				"eg: .wassabee findpassword --wallet:/home/user/.wasabiwallet/client/Wallets/my-wallet.json --numbers:false --symbold:true",
				"",
				{ "w|wallet=", "The path to the wallet file.",
					x =>  Wallet = x },
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
				else if (string.IsNullOrEmpty(Wallet))
				{
					Console.WriteLine("Missing required argument `--wallet=wallet-file-path`.");
					Console.WriteLine("Use `findpassword --help` for details.");
					error = true;
				}
				else if (!PasswordFinder.Charsets.ContainsKey(Language))
				{
					Console.WriteLine($"`{Language}` is not available language try with `en, es, pt, it or fr`.");
					Console.WriteLine("Use `findpassword --help` for details.");
					error = true;
				}
				else
				{
					var km = KeyManager.FromFile(Wallet);
					PasswordFinder.Find(km.EncryptedSecret.ToWif(), Language, UseNumbers, UseSymbols);
				}
			}
			catch (Exception)
			{
				Console.WriteLine($"There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return Task.FromResult(0);
		}
	}
}
