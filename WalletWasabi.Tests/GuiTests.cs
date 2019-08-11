using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests
{
	public class GuiTests
	{
		public GuiTests(ITestOutputHelper helper)
		{
			var logsPath = Path.Combine(".", "Logs.txt");
			if (File.Exists(logsPath))
			{
				File.Delete(logsPath);
			}
			Logger.SetFilePath(logsPath);
			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		[Fact]
		public async Task CanInitializeWalletGUIAsync()
		{
			using (var tester = GuiTester.Create())
			{
				var client1 = tester.CreateGuiClient();
				var client2 = tester.CreateGuiClient();
				await tester.StartAllAsync();
			}
		}
	}
}
