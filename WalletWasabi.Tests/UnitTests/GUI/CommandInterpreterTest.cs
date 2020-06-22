using Mono.Options;
using System;
using System.IO;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.GUI
{
	public class CommandInterpreterTest
	{
		[Fact]
		public async void ShowsHelpAsync()
		{
			(StringWriter outW, StringWriter errorW, CommandInterpreter c) = Make();
			await c.ExecuteCommandsAsync(new string[] { "--help" }, new Command("mix"), new Command("findpassword"), new Command("crashreport"));

			Assert.Equal(
				@"Usage: wassabee [OPTIONS]+
Launches Wasabi Wallet.

  -h, --help                 Displays help page and exit.
  -v, --version              Displays Wasabi version and exit.

Available commands are:

        mix
        findpassword
        crashreport
",
				outW.ToString(),
				new StringNoWhiteSpaceEqualityComparer());

			Assert.Equal("", errorW.ToString());
		}

		[Fact]
		public async void ShowsVersionAsync()
		{
			(StringWriter outW, StringWriter errorW, CommandInterpreter c) = Make();
			await c.ExecuteCommandsAsync(new string[] { "--version" }, new Command("mix"), new Command("findpassword"), new Command("crashreport"));

			Assert.Equal(
				$@"Wasabi Client Version: {Constants.ClientVersion}
Compatible Coordinator Version: {Constants.ClientSupportBackendVersionText}
Compatible Bitcoin Core and Bitcoin Knots Versions: {Constants.BitcoinCoreVersion}
Compatible Hardware Wallet Interface Version: {Constants.HwiVersion}
",
				outW.ToString(),
				new StringNoWhiteSpaceEqualityComparer());

			Assert.Equal("", errorW.ToString());
		}

		[Fact]
		public async void InvalidCommandAsync()
		{
			(StringWriter outW, StringWriter errorW, CommandInterpreter c) = Make();
			await c.ExecuteCommandsAsync(new string[] { "invalid" }, new Command("mix"), new Command("findpassword"), new Command("crashreport"));

			Assert.Equal("", outW.ToString());
			Assert.Equal(
				@"wassabee: Unknown command: invalid
wassabee: Use `wassabee help` for usage.
",
				errorW.ToString());
		}

		[Fact]
		public async void InvalidCommandWithHelpAsync()
		{
			(StringWriter outW, StringWriter errorW, CommandInterpreter c) = Make();
			await c.ExecuteCommandsAsync(new string[] { "invalid", "--help" }, new Command("mix"), new Command("findpassword"), new Command("crashreport"));

			Assert.Equal(
				$@"Wasabi Client Version: {Constants.ClientVersion}
Compatible Coordinator Version: {Constants.ClientSupportBackendVersionText}
Compatible Bitcoin Core and Bitcoin Knots Versions: {Constants.BitcoinCoreVersion}
Compatible Hardware Wallet Interface Version: {Constants.HwiVersion}
Usage: wassabee [OPTIONS]+
Launches Wasabi Wallet.

  -h, --help                 Displays help page and exit.
  -v, --version              Displays Wasabi version and exit.

Available commands are:

        mix
        findpassword
        crashreport
",
				outW.ToString(),
				new StringNoWhiteSpaceEqualityComparer());

			Assert.Equal(
				@"wassabee: Unknown command: invalid
wassabee: Use `wassabee help` for usage.
",
				errorW.ToString());
		}

		private static (StringWriter, StringWriter, CommandInterpreter) Make()
		{
			var outW = new StringWriter();
			var errorW = new StringWriter();
			return (outW, errorW, new CommandInterpreter(outW, errorW));
		}
	}
}
