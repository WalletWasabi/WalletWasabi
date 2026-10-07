using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using WalletWasabi.Io;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

[CollectionDefinition("Directory durability", DisableParallelization = true)]
public class DirectoryDurabilityCollection;

[Collection("Directory durability")]
public class DirectoryDurabilityTests
{
	[Fact]
	public void FlushesRenamedFilesWithoutLeakingDirectoryHandles()
	{
		Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsAndroid(), "Native directory durability requires Linux or Android.");
		var directory = Path.Combine(Path.GetTempPath(), "wasabi-durability-" + Guid.NewGuid().ToString("N"), "日本語");
		Directory.CreateDirectory(directory);
		try
		{
			var destination = Path.Combine(directory, "saved.dat");
			File.WriteAllText(destination, "public storage test");
			DirectoryDurability.FlushContainingDirectory(destination);
			var handles = Directory.EnumerateFiles("/proc/self/fd").Count();
			for (var i = 0; i < 64; i++)
			{
				File.WriteAllText(destination + ".new", "public replacement " + i);
				File.Move(destination + ".new", destination, overwrite: true);
				DirectoryDurability.FlushContainingDirectory(destination);
			}
			Assert.Equal("public replacement 63", File.ReadAllText(destination));
			Assert.Equal(handles, Directory.EnumerateFiles("/proc/self/fd").Count());
		}
		finally { Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true); }
	}

	[Fact]
	public void InvalidParentFailsInsteadOfReportingDurability()
	{
		Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsAndroid(), "Native directory durability requires Linux or Android.");
		var directory = Path.Combine(Path.GetTempPath(), "wasabi-durability-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var regularFile = Path.Combine(directory, "not-a-directory");
			File.WriteAllText(regularFile, "public storage test");
			var error = Assert.Throws<IOException>(() => DirectoryDurability.FlushContainingDirectory(Path.Combine(regularFile, "child")));
			Assert.IsType<Win32Exception>(error.InnerException);
			Assert.Throws<IOException>(() => DirectoryDurability.FlushContainingDirectory(Path.Combine(directory, "missing", "child")));
		}
		finally { Directory.Delete(directory, recursive: true); }
	}
}
