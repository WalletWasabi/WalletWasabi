using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class EnvironmentHelpersTests
{
	[Fact]
	public async Task CreateOwnerOnlyDirectoryAsync()
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}

		var workDir = await Common.GetEmptyWorkDirAsync();
		const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
		const UnixFileMode WorldRead = UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

		// A new directory is created owner-only.
		var created = Path.Combine(workDir, "created");
		EnvironmentHelpers.CreateOwnerOnlyDirectory(created);
		Assert.Equal(OwnerOnly, File.GetUnixFileMode(created));
		Assert.False(EnvironmentHelpers.WarnIfWorldAccessible(created));

		// An existing directory keeps its mode and is only reported.
		var existing = Path.Combine(workDir, "existing");
		Directory.CreateDirectory(existing);
		File.SetUnixFileMode(existing, OwnerOnly | WorldRead); // Not subject to the umask, unlike CreateDirectory(path, mode).
		EnvironmentHelpers.CreateOwnerOnlyDirectory(existing);
		Assert.Equal(OwnerOnly | WorldRead, File.GetUnixFileMode(existing));
		Assert.True(EnvironmentHelpers.WarnIfWorldAccessible(existing));
	}
}
