using System;
using System.IO;
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
		[Fact]
		public async Task ModifyStartupOnDifferentSystemsTestAsync()
		{
			UiConfig originalConfig = GetUiConfig();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);
				await StartupHelper.ModifyStartupSettingAsync(false);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				await StartupHelper.ModifyStartupSettingAsync(true);
				await StartupHelper.ModifyStartupSettingAsync(false);
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
	}
}
