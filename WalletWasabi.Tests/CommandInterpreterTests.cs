using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Mono.Options;
using WalletWasabi.Gui.CommandLine;
using Xunit;

namespace WalletWasabi.Tests
{
	public class CommandInterpreterTests
	{
		private static Command CreateMixCommand() => new Command("mix");
		private static Command CreateFindPasswordCommand() => new Command("findpassword");

		[Fact]
		// When invoked without any argument it should launch in GUI mode
		public async Task InvokedWithoutArgumentsAsync()
		{
			var output = new StreamWriter(new MemoryStream(), Encoding.ASCII, 1024, true);
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);

			var runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new string[0]);
			Assert.True(runGui);
		}

		[Fact]
		// When invoked with "help" (or "--help") it should NOT in GUI mode
		public async Task InvokedWithHelpAsync()
		{
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "help" });
			output.Flush();
			var consoleOutput = Encoding.ASCII.GetString(ms.ToArray());
			Assert.Contains("Usage:", consoleOutput);
			Assert.False(runGui);

			ms.Flush();
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);
			runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "--help" });
			output.Flush();
			consoleOutput = Encoding.ASCII.GetString(ms.ToArray());
			Assert.Contains("Usage:", consoleOutput);
			Assert.False(runGui);
		}

		[Fact]
		// When invoked with "--version" it should NOT in GUI mode
		public async Task InvokedWithVersionAsync()
		{
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "--version" });
			output.Flush();
			var consoleOutput = Encoding.ASCII.GetString(ms.ToArray());
			Assert.Contains("Wasabi Client Version:", consoleOutput);
			Assert.False(runGui);
		}

		[Fact]
		// When invoked with "--datadir" only it should set the DataDir and launch the GUI
		public async Task InvokedWithDataDirAsync()
		{
			var global = new WalletWasabi.Gui.Global();
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(global, new[]{ "--datadir", "expected/passed/datadir" });
			output.Flush();
			Assert.Equal("expected/passed/datadir", global.DataDir);
			Assert.True(runGui);
		}

		[Fact]
		// When invoked with "--datadir" only it should set the DataDir and launch the GUI
		public async Task InvokedWithMixAsync()
		{
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			var invoked = false;
			var mixCommand = CreateMixCommand(); 
			mixCommand.Run = (s)=> invoked = true;
			CommandInterpreter.Configure(mixCommand, CreateFindPasswordCommand(), output, output);
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "mix" });
			Assert.True(invoked);
			Assert.False(runGui);

			// Ensure backward compatibility with --mix
			mixCommand = CreateMixCommand(); 
			mixCommand.Run = (s)=> invoked = true;
			CommandInterpreter.Configure(mixCommand, CreateFindPasswordCommand(), output, output);
			invoked = false;
			runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "--mix" });
			Assert.True(invoked);
			Assert.False(runGui);
		}

		[Fact]
		// When invoked with "findpassword" it should NOT launch the GUI
		public async Task InvokedWithPasswordFinderAsync()
		{
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			var invoked = false;
			var findPasswordCommand = CreateFindPasswordCommand(); 
			findPasswordCommand.Run = (s)=> invoked = true;
			CommandInterpreter.Configure(CreateMixCommand(), findPasswordCommand, output, output);
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "findpassword" });
			Assert.True(invoked);
			Assert.False(runGui);
		}

		[Fact]
		// When invoked with "--datadir" and other command "mix or findpassword" it should 
		// set the DataDir but not launch the GUI.
		public async Task InvokedWithDataDirAndMixAsync()
		{
			var global = new WalletWasabi.Gui.Global();
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			var invoked = false;
			var mixCommand = CreateMixCommand(); 
			mixCommand.Run = (s)=> invoked = true;
			CommandInterpreter.Configure(mixCommand, CreateFindPasswordCommand(), output, output);
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(global, new[]{ "mix", "--datadir", "expected/passed/datadir" });
			output.Flush();
			Assert.Equal("expected/passed/datadir", global.DataDir);
			Assert.True(invoked);
			Assert.False(runGui);
		}

		[Fact]
		// When invoked with and unknown command it should not launch the GUI.
		public async Task InvokedWithUnknownCommandAsync()
		{
			var ms = new MemoryStream();
			var output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);
			// unknown command
			var runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "unknowncommand"});
			output.Flush();
			var consoleOutput = Encoding.ASCII.GetString(ms.ToArray());
			Assert.Contains("Unknown command:", consoleOutput);
			Assert.False(runGui);

			ms = new MemoryStream();
			output = new StreamWriter(ms, Encoding.ASCII, 1024, true);
			// Unknown option
			CommandInterpreter.Configure(CreateMixCommand(), CreateFindPasswordCommand(), output, output);
			runGui = await CommandInterpreter.ExecuteCommandsAsync(null, new[]{ "--unknownoption"});
			output.Flush();
			consoleOutput = Encoding.ASCII.GetString(ms.ToArray());
			Assert.Contains("Unknown command:", consoleOutput);
			Assert.False(runGui);
		}
	}
}