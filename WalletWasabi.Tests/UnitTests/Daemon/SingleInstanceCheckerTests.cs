using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Client;
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

	[Fact]
	public async Task RestartedInstanceWaitsForPreviousInstanceAsync()
	{
		var workDir = await Common.GetEmptyWorkDirAsync();

		var previous = new SingleInstanceChecker(workDir);
		Assert.True(previous.IsFirstInstance());

		// Repeated attempts must not break the lock held by the running instance.
		using var restarted = new SingleInstanceChecker(workDir);
		Assert.False(restarted.IsFirstInstance(TimeSpan.FromMilliseconds(600)));

		// Once the previous instance shuts down, the waiting instance takes over.
		var waiting = Task.Run(() => restarted.IsFirstInstance(TimeSpan.FromSeconds(10)));
		await Task.Delay(300);
		previous.Dispose();
		Assert.True(await waiting);
	}
}
