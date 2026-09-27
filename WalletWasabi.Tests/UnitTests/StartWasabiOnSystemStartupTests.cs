using Microsoft.Win32;
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
	[Theory]
	[InlineData(null)]
	[InlineData("\"C:\\Program Files\\WasabiWallet\\wassabee.exe\" startsilent")]
	public void ModifyWindowsStartupPreservesExistingEntries(string? existingCommand)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return;
		}

		// Never register the test executable in the developer's actual startup settings.
		string keyPath = $"SOFTWARE\\WalletWasabi.Tests\\Startup\\{Guid.NewGuid():N}";
		try
		{
			using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
			key.SetValue("OtherApplication", "unchanged");
			if (existingCommand is not null)
			{
				key.SetValue(nameof(WalletWasabi), existingCommand);
			}

			string expectedCommand = existingCommand ?? $"{EnvironmentHelpers.GetExecutablePath()} {StartupHelper.SilentArgument}";
			WindowsStartupHelper.AddOrRemoveRegistryKey(true, keyPath);
			Assert.Equal(expectedCommand, key.GetValue(nameof(WalletWasabi)));

			WindowsStartupHelper.AddOrRemoveRegistryKey(true, keyPath);
			Assert.Equal(expectedCommand, key.GetValue(nameof(WalletWasabi)));

			WindowsStartupHelper.AddOrRemoveRegistryKey(false, keyPath);
			Assert.Null(key.GetValue(nameof(WalletWasabi)));

			WindowsStartupHelper.AddOrRemoveRegistryKey(false, keyPath);
			Assert.Null(key.GetValue(nameof(WalletWasabi)));
			Assert.Equal("unchanged", key.GetValue("OtherApplication"));
		}
		finally
		{
			Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
		}
	}

	[Fact]
	public async Task ModifyStartupOnDifferentSystemsTestAsync()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return;
		}

		UiConfig originalConfig = GetUiConfig();
		try
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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
			// Restore original setting for developers.
			await StartupHelper.ModifyStartupSettingAsync(originalConfig.RunOnSystemStartup);
		}
	}

	[Fact]
	public async Task RunOnSystemStartupGetsSetCorrectlyAsync()
	{
		// Imitate fresh UiConfig file.
		string workDir = await Common.GetEmptyWorkDirAsync();

		UiConfig config = UiConfig.LoadFile(Path.Combine(workDir, "UiConfig.json"));
		Assert.True(config.Oobe);
		Assert.False(config.RunOnSystemStartup);
	}

	private UiConfig GetUiConfig()
	{
		string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
		return UiConfig.LoadFile(Path.Combine(dataDir, "UiConfig.json"));
	}
}
