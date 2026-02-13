using System.IO;
using WalletWasabi.WabiSabi.Client.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoordinatorPrisonTests
{
	[Fact]
	public void BanAndCheckTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"{nameof(CoordinatorPrisonTests)}_{nameof(BanAndCheckTest)}_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			Assert.False(prison.IsBanned("coordinator.example.com", out _));

			prison.Ban("coordinator.example.com", "Served inconsistent round data");

			Assert.True(prison.IsBanned("coordinator.example.com", out var reason));
			Assert.Equal("Served inconsistent round data", reason);
			Assert.False(prison.IsBanned("other-coordinator.example.com", out _));

			// Case insensitive.
			Assert.True(prison.IsBanned("COORDINATOR.EXAMPLE.COM", out _));

			// Leading/trailing whitespace should not match.
			Assert.False(prison.IsBanned(" coordinator.example.com", out _));
			Assert.False(prison.IsBanned("coordinator.example.com ", out _));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void PersistenceTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"{nameof(CoordinatorPrisonTests)}_{nameof(PersistenceTest)}_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			// Ban coordinator and let prison go out of scope.
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			prison.Ban("evil-coordinator.onion", "Lied about inputs");

			// Load from file - ban should persist.
			var reloaded = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.True(reloaded.IsBanned("evil-coordinator.onion", out var reason));
			Assert.Equal("Lied about inputs", reason);
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void EmptyPrisonLoadsCorrectlyTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"{nameof(CoordinatorPrisonTests)}_{nameof(EmptyPrisonLoadsCorrectlyTest)}_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.False(prison.IsBanned("any-coordinator.com", out _));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void CorruptFileRecoveryTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"{nameof(CoordinatorPrisonTests)}_{nameof(CorruptFileRecoveryTest)}_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			// Write corrupt data.
			File.WriteAllText(Path.Combine(tempDir, "BannedCoordinators.json"), "not valid json{{{");

			// Should recover gracefully.
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.False(prison.IsBanned("any-coordinator.com", out _));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void DuplicateBanIsIgnoredTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"{nameof(CoordinatorPrisonTests)}_{nameof(DuplicateBanIsIgnoredTest)}_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			prison.Ban("coordinator.example.com", "First reason");
			prison.Ban("coordinator.example.com", "Second reason");

			Assert.True(prison.IsBanned("coordinator.example.com", out var reason));
			Assert.Equal("First reason", reason);
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}

	[Fact]
	public void MultipleBannedCoordinatorsTest()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"{nameof(CoordinatorPrisonTests)}_{nameof(MultipleBannedCoordinatorsTest)}_{Guid.NewGuid()}");
		Directory.CreateDirectory(tempDir);
		try
		{
			var prison = CoordinatorPrison.CreateOrLoadFromFile(tempDir);

			prison.Ban("evil-coordinator-1.onion", "Inconsistent round data");
			prison.Ban("evil-coordinator-2.onion", "Lied about inputs");

			Assert.True(prison.IsBanned("evil-coordinator-1.onion", out _));
			Assert.True(prison.IsBanned("evil-coordinator-2.onion", out _));
			Assert.False(prison.IsBanned("honest-coordinator.onion", out _));

			// Reload from file - both bans should persist.
			var reloaded = CoordinatorPrison.CreateOrLoadFromFile(tempDir);
			Assert.True(reloaded.IsBanned("evil-coordinator-1.onion", out _));
			Assert.True(reloaded.IsBanned("evil-coordinator-2.onion", out _));
			Assert.False(reloaded.IsBanned("honest-coordinator.onion", out _));
		}
		finally
		{
			Directory.Delete(tempDir, true);
		}
	}
}
