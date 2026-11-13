using NBitcoin;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using WalletWasabi.Storages;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Storages;

/// <summary>
/// Tests for <see cref="FilterManager"/>.
/// </summary>
public class FilterManagerTests
{
	[Fact]
	public async Task FilterManagerTestsAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		var directory = await Common.GetEmptyWorkDirAsync();

		using var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(directory, "Shared.sqlite"));
		var blockFilterSqliteStorage = new BlockFilterSqliteStorage(sharedSqliteStorage.GetConnectionFactory());

		var filterManager = new FilterManager(Network.Main, new FilterHeaderChain(), blockFilterSqliteStorage, new EventBus());
		await filterManager.InitializeAsync(0, testCts.Token);

		// Remove starting filter.
		FilterModel? filterModel = await filterManager.TryRemoveLastFilterAsync();
		Assert.NotNull(filterModel);

		// No filter to remove.
		filterModel = await filterManager.TryRemoveLastFilterAsync();
		Assert.Null(filterModel);
	}
}
