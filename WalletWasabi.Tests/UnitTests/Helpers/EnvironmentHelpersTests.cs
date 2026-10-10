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
		const UnixFileMode GroupRead = UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
		const UnixFileMode WorldRead = UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

		// A new directory is created owner-only.
		var created = Path.Combine(workDir, "created");
		EnvironmentHelpers.CreateOwnerOnlyDirectory(created);
		Assert.Equal(OwnerOnly, File.GetUnixFileMode(created));

		// An existing directory loses world access but keeps what the operator granted to the group.
		var existing = Path.Combine(workDir, "existing");
		Directory.CreateDirectory(existing, OwnerOnly | GroupRead | WorldRead);
		EnvironmentHelpers.CreateOwnerOnlyDirectory(existing);
		Assert.Equal(OwnerOnly | GroupRead, File.GetUnixFileMode(existing));
	}
}
