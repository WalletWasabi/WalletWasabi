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
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class StartWasabiOnSystemStartupTests
	{
		private const string PathToRegistyKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		private string _listCmd = $"osascript -e \' tell application \"System Events\" to get every login item\'";

		// Path to Wasabi.desktop file. Only Linux distributions need this.
		private string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "Wasabi.desktop");

		private string _expectedDesktopFileContent = string.Join(
					"\n",
					"[Desktop Entry]",
					$"Name={Constants.AppName}",
					"Type=Application",
					$"Exec={EnvironmentHelpers.GetExecutablePath()}",
					"Hidden=false",
					"Terminal=false",
					"X-GNOME-Autostart-enabled=true");

		[Fact]
		public async Task ModifyStartupOnDifferentSystemsTestAsync()
		{
			UiConfig originalConfig = GetUiConfig();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);

				Assert.True(RegistryKeyExists());

				await StartupHelper.ModifyStartupSettingAsync(false);

				Assert.False(RegistryKeyExists());
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);

				Assert.True(File.Exists(_filePath));
				Assert.Equal(_expectedDesktopFileContent, GetFileContent(_filePath));

				await StartupHelper.ModifyStartupSettingAsync(false);

				Assert.False(File.Exists(_filePath));
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

		private bool RegistryKeyExists()
		{
			bool result = false;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(PathToRegistyKey, false);
				result = registryKey.GetValueNames().Contains(nameof(WalletWasabi));
			}

			return result;
		}

		private string GetFileContent(string filePath)
		{
			return string.Join("\n", File.ReadAllLines(filePath));
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

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			string output = process.StandardOutput.ReadToEnd();  // Gives back "login items Wasabi Wallet" or "login items"

			return output;
		}
	}
}
