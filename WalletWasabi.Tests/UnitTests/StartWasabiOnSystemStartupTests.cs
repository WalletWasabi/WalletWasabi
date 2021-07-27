using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Gui;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class StartWasabiOnSystemStartupTests
	{
		private readonly WindowsStartupHelper _windowsHelper = new();

		private readonly MacOsStartupHelper _macOsHelper = new();

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

				//string loginItems = await _macOsHelper.GetLoginItemsAsync();

				//Assert.Contains(Constants.AppName, loginItems);

				await StartupHelper.ModifyStartupSettingAsync(false);

				//loginItems = await _macOsHelper.GetLoginItemsAsync();

				//Assert.DoesNotContain(Constants.AppName, loginItems);
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
	}
}
