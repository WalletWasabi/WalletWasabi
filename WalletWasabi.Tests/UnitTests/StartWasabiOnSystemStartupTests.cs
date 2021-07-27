using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Gui;
using WalletWasabi.Helpers;
using WalletWasabi.Microservices;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class StartWasabiOnSystemStartupTests
	{
		private string _listCmd = $"osascript -e \' tell application \"System Events\" to get every login item\'";

		private readonly WindowsStartupHelper _windowsHelper = new();

		[Fact]
		public async Task ModifyStartupOnDifferentSystemsTestAsync()
		{
			UiConfig originalConfig = GetUiConfig();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);

				Assert.True(_windowsHelper.RegistryKeyExists());

				await StartupHelper.ModifyStartupSettingAsync(false);

				Assert.False(_windowsHelper.RegistryKeyExists());
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);

				Assert.True(File.Exists(LinuxStartupHelper.FilePath));
				Assert.Equal(LinuxStartupHelper.ExpectedDesktopFileContent, LinuxStartupHelper.GetFileContent());

				await StartupHelper.ModifyStartupSettingAsync(false);

				Assert.False(File.Exists(LinuxStartupHelper.FilePath));
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);

				string loginItems = await GetLoginItemsAsync(_listCmd);

				Assert.Contains(Constants.AppName, loginItems);

				await StartupHelper.ModifyStartupSettingAsync(false);

				loginItems = await GetLoginItemsAsync(_listCmd);

				Assert.DoesNotContain(Constants.AppName, loginItems);
			}

			// Restore original setting for devs.
			await StartupHelper.ModifyStartupSettingAsync(originalConfig.RunOnSystemStartup);
		}

		private UiConfig GetUiConfig()
		{
			string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
			uiConfig.LoadOrCreateDefaultFile();

			return uiConfig;
		}

		private async Task<string> GetLoginItemsAsync(string cmd)
		{
			var escapedArgs = cmd.Replace("\"", "\\\"");

			var startInfo = new ProcessStartInfo
			{
				FileName = "/usr/bin/env",
				Arguments = $"sh -c \"{escapedArgs}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			using var process = new ProcessAsync(startInfo);

			process.Start();

			string output = process.StandardOutput.ReadToEnd();  // Gives back "login item Wasabi Wallet" or "login item"

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			return output;
		}
	}
}
