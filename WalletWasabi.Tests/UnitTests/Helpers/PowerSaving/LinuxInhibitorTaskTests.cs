using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers.PowerSaving;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers.PowerSaving;

/// <summary>
/// Tests for <see cref="LinuxInhibitorTask"/> class.
/// </summary>
public class LinuxInhibitorTaskTests
{
	[Fact]
	public async Task TestAvailabilityAsync()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			bool isAvailable = await LinuxInhibitorTask.IsSystemdInhibitSupportedAsync();
			Assert.True(isAvailable);
		}
	}
}
