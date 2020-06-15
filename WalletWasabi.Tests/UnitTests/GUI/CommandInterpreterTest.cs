using Mono.Options;
using System.IO;
using WalletWasabi.Gui.CommandLine;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
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
				@"Usage: wassabee [OPTIONS]+
Launches Wasabi Wallet.

  -h, --help                 Displays help page and exit.
  -v, --version              Displays Wasabi version and exit.

Available commands are:

        mixer               
        findpassword        
",
				textWriter.ToString()
			);
		}
	}
}
