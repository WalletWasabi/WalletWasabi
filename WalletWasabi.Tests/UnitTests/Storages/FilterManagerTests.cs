using NBitcoin;
using System.IO;
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
	[Fact(Timeout = 60000)]
	public async Task FilterManagerTestsAsync()
	{
		var directory = await Common.GetEmptyWorkDirAsync();

		using var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(directory, "Shared.sqlite"));
		var blockFilterSqliteRepository = new BlockFilterSqliteRepository(sharedSqliteStorage.GetConnectionFactory());

		var filterManager = new FilterManager(Network.Main, new FilterHeaderChain(), blockFilterSqliteRepository, new EventBus());
		await filterManager.InitializeAsync(0, TestContext.Current.CancellationToken);

		// Remove starting filter.
		FilterModel? filterModel = await filterManager.TryRemoveLastFilterAsync();
		Assert.NotNull(filterModel);

		// No filter to remove.
		filterModel = await filterManager.TryRemoveLastFilterAsync();
		Assert.Null(filterModel);
	}
}
