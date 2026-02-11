using System.IO;
using WalletWasabi.WabiSabi.Client.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoordinatorPrisonTests
{
	[Fact]
	public void BanAndCheckTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			Assert.False(prison.IsBanned("coordinator.example.com"));

			prison.Ban("coordinator.example.com", "Served inconsistent round data");

			Assert.True(prison.IsBanned("coordinator.example.com"));
			Assert.False(prison.IsBanned("other-coordinator.example.com"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void BanIsCaseInsensitiveTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			prison.Ban("Coordinator.Example.COM", "Test reason");

			Assert.True(prison.IsBanned("coordinator.example.com"));
			Assert.True(prison.IsBanned("COORDINATOR.EXAMPLE.COM"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void PersistenceTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			// Ban coordinator and let prison go out of scope.
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			prison.Ban("evil-coordinator.onion", "Lied about inputs");

			// Load from file - ban should persist.
			var reloaded = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.True(reloaded.IsBanned("evil-coordinator.onion"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void EmptyPrisonLoadsCorrectlyTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.False(prison.IsBanned("any-coordinator.com"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void CorruptFileRecoveryTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			// Write corrupt data.
			File.WriteAllText(Path.Combine(tempDir, "BannedCoordinators.json"), "not valid json{{{");

			// Should recover gracefully.
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.False(prison.IsBanned("any-coordinator.com"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void DuplicateBanIsIgnoredTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			prison.Ban("coordinator.example.com", "First reason");
			prison.Ban("coordinator.example.com", "Second reason");

			Assert.True(prison.IsBanned("coordinator.example.com"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void MultipleBannedCoordinatorsTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"CoordinatorPrisonTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			prison.Ban("evil-coordinator-1.onion", "Inconsistent round data");
			prison.Ban("evil-coordinator-2.onion", "Lied about inputs");

			Assert.True(prison.IsBanned("evil-coordinator-1.onion"));
			Assert.True(prison.IsBanned("evil-coordinator-2.onion"));
			Assert.False(prison.IsBanned("honest-coordinator.onion"));

			// Reload from file - both bans should persist.
			var reloaded = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.True(reloaded.IsBanned("evil-coordinator-1.onion"));
			Assert.True(reloaded.IsBanned("evil-coordinator-2.onion"));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}
}
