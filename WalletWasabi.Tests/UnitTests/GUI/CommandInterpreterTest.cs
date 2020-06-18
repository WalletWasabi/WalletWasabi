using Mono.Options;
using System.IO;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.GUI
{
	public class CommandInterpreterTest
	{
		[Fact]
		public async void CommandInterpreterShowsHelpAsync()
		{
			var textWriter = new StringWriter();
			var c = new CommandInterpreter(textWriter);
			await c.ExecuteCommandsAsync(new string[] { "wassabee", "--help" }, new Command("mixer"), new Command("findpassword"));

			Assert.Equal(
				$@"Wasabi Client Version: {Constants.ClientVersion}
Compatible Coordinator Version: {Constants.ClientSupportBackendVersionText}
Compatible Bitcoin Core and Bitcoin Knots Versions: {Constants.BitcoinCoreVersion}
Compatible Hardware Wallet Interface Version: {Constants.HwiVersion}

Usage: wassabee [OPTIONS]+
Launches Wasabi Wallet.

Options:
Usage: wassabee [OPTIONS]+
Launches Wasabi Wallet.

  -h, --help                 Displays help page and exit.
  -v, --version              Displays Wasabi version and exit.

Available commands are:

        mixer               
        findpassword        
",
				textWriter.ToString());
		}
	}
}
