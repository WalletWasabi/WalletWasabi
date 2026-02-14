using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Daemon;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Daemon;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class SingleInstanceCheckerTests
{
	[Fact]
	public async Task SingleInstanceTestsAsync()
	{
		var workDir = await Common.GetEmptyWorkDirAsync();
		string? path = null;

		using (var checker = new SingleInstanceChecker(workDir))
		{
			Assert.True(checker.IsFirstInstance());
			Assert.True(File.Exists(checker.LockFilePath), "Lock file should be created");
			path = checker.LockFilePath;

			using var checker2 = new SingleInstanceChecker(workDir);
			Assert.False(checker2.IsFirstInstance());
			Assert.True(File.Exists(checker2.LockFilePath), "Lock file should be created");
		}

		// Checker deletes the lock file after it is disposed. Assert the behavior.
		Assert.NotNull(path);
		Assert.False(File.Exists(path), "Lock file should no longer exist");
	}
}
