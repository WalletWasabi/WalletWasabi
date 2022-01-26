using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class StartWasabiOnSystemStartupTests
{
	private readonly WindowsStartupTestHelper _windowsHelper = new();

	[Fact]
	public async Task ModifyStartupOnDifferentSystemsTestAsync()
	{
		UiConfig originalConfig = GetUiConfig();
		try
		{
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

				Assert.True(File.Exists(LinuxStartupTestHelper.FilePath));
				Assert.Equal(LinuxStartupTestHelper.ExpectedDesktopFileContent, LinuxStartupTestHelper.GetFileContent());

				await StartupHelper.ModifyStartupSettingAsync(false);

				Assert.False(File.Exists(LinuxStartupTestHelper.FilePath));
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// We don't read back the results, because on the CI pipeline, we cannot hit the "Allow" option of the pop-up window,
				// which comes up when a third-party app wants to modify the Login Items.

				await StartupHelper.ModifyStartupSettingAsync(true);

				await StartupHelper.ModifyStartupSettingAsync(false);
			}
		}
		finally
		{
			// Restore original setting for devs.
			await StartupHelper.ModifyStartupSettingAsync(originalConfig.RunOnSystemStartup);
		}
	}

	private UiConfig GetUiConfig()
	{
		string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
		UiConfig uiConfig = new(Path.Combine(dataDir, "UiConfig.json"));
		uiConfig.LoadOrCreateDefaultFile();

		return uiConfig;
	}
}
