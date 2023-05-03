using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Stores;

/// <summary>
/// Tests for <see cref="IndexStore"/>.
/// </summary>
public class IndexStoreTests
{
	[Fact]
	public async Task IndexStoreTestsAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var network = Network.Main;

		var dir = (await GetIndexStorePathsAsync()).dir;
		if (Directory.Exists(dir))
		{
			Directory.Delete(dir, true);
		}
		await using var indexStore = new IndexStore(dir, network, new SmartHeaderChain());
		await indexStore.InitializeAsync(testDeadlineCts.Token);
	}

	private async Task<(string dir, string matureFilters, string immatureFilters)> GetIndexStorePathsAsync([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		var dir = Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), "IndexStore");
		await IoHelpers.TryDeleteDirectoryAsync(dir);
		var matureFilters = Path.Combine(dir, "MatureIndex.dat");
		var immatureFilters = Path.Combine(dir, "ImmatureIndex.dat");
		IoHelpers.EnsureContainingDirectoryExists(matureFilters);
		IoHelpers.EnsureContainingDirectoryExists(immatureFilters);
		return (dir, matureFilters, immatureFilters);
	}
}
