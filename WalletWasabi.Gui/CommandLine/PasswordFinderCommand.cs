using Mono.Options;
using System;
using System.Collections.Generic;

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

			Options = new OptionSet() {
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
			catch (Exception)
			{
				Console.WriteLine($"commands: There was a problem interpreting the command, please review it.");
				error = true;
			}
			Environment.Exit(error ? 1 : 0);
			return 0;
		}
	}
}
