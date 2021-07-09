using System;
using System.Runtime.InteropServices;
using WalletWasabi.Fluent.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class StartWasabiOnSystemStartupTests
	{
		[Fact]
		public void ModifyStartupOnDifferentSystemsTest()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				StartupHelper.ModifyStartupSetting(true);
				StartupHelper.ModifyStartupSetting(false);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				Assert.Throws<NotImplementedException>(() => StartupHelper.ModifyStartupSetting(true));
				Assert.Throws<NotImplementedException>(() => StartupHelper.ModifyStartupSetting(false));
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				Assert.Throws<NotImplementedException>(() => StartupHelper.ModifyStartupSetting(true));
				Assert.Throws<NotImplementedException>(() => StartupHelper.ModifyStartupSetting(false));
			}
		}
	}
}
