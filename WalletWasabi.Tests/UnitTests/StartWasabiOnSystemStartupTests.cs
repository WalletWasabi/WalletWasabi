using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Gui;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class StartWasabiOnSystemStartupTests
	{
		private const string PathToRegistyKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

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
				await StartupHelper.ModifyStartupSettingAsync(false);
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
	}
}
