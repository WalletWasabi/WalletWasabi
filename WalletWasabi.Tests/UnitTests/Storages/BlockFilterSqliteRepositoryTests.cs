using NBitcoin;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Storages;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Storages;

/// <summary>
/// Tests for <see cref="BlockFilterSqliteRepository"/>.
/// </summary>
public class BlockFilterSqliteRepositoryTests
{
	private static byte[] DummyFilterData = Convert.FromHexString("02832810ec08a0");

	[Fact]
	public async Task TryAppendAsync()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();

		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);

		var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(workDir, "Shared.sqlite"));
		var filterStorage = new BlockFilterSqliteRepository(sharedSqliteStorage.GetConnectionFactory());

		bool added = filterStorage.TryAppend(filter0);
		Assert.True(added);

		added = filterStorage.TryAppend(filter1);
		Assert.True(added);

		// The filter with the same block height is already present.
		added = filterStorage.TryAppend(filter1);
		Assert.False(added);
	}

	[Fact]
	public async Task TryRemoveLastAsync()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();

		FilterModel startingFilter = FilterCheckpoints.GetWasabiGenesisFilter(Network.Main);

		var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(workDir, "Shared.sqlite"));
		var filterStorage = new BlockFilterSqliteRepository(sharedSqliteStorage.GetConnectionFactory());
		filterStorage.TryAppend(startingFilter);

		bool result = filterStorage.TryRemoveLast(out FilterModel? filter1);
		Assert.True(result);
		Assert.NotNull(filter1);
		Assert.NotSame(startingFilter, filter1); // The filter was stored in the database and removed from the database, so no reference equality.

		result = filterStorage.TryRemoveLast(out FilterModel? filter2);
		Assert.False(result);
		Assert.Null(filter2);
	}

	[Fact]
	public async Task AppendAndRemoveAsync()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();

		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);

		var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(workDir, "Shared.sqlite"));
		var filterStorage = new BlockFilterSqliteRepository(sharedSqliteStorage.GetConnectionFactory());
		filterStorage.TryAppend(filter0);

		bool added = filterStorage.TryAppend(filter1);
		Assert.True(added);

		bool result = filterStorage.TryRemoveLast(out FilterModel? filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.NotSame(filter1, filterLast);
		Assert.Equal(1u, filterLast.Header.Height.Height);
		Assert.Equal(new uint256(2), filterLast.Header.BlockHash);
		Assert.Equal(uint256.One, filterLast.Header.BlockFilterHeader);
		Assert.Equal(1231006506, filterLast.Header.EpochBlockTime);
		Assert.Equal(DummyFilterData, filterLast.Filter.ToBytes());

		result = filterStorage.TryRemoveLast(out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(0u, filterLast.Header.Height.Height);
		Assert.Equal(uint256.One, filterLast.Header.BlockHash);
		Assert.Equal(uint256.Zero, filterLast.Header.BlockFilterHeader);
		Assert.Equal(1231006505, filterLast.Header.EpochBlockTime);
		Assert.Equal(DummyFilterData, filterLast.Filter.ToBytes());
	}

	[Fact]
	public async Task TryRemoveLastIfNewerThanAsync()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();

		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);
		FilterModel filter2 = CreateFilterModel(blockHeight: 2, blockHash: new uint256(3), filterData: DummyFilterData, headerOrPrevBlockHash: new uint256(2), blockTime: 1231006507);
		FilterModel filter3 = CreateFilterModel(blockHeight: 3, blockHash: new uint256(4), filterData: DummyFilterData, headerOrPrevBlockHash: new uint256(3), blockTime: 1231006508);

		var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(workDir, "Shared.sqlite"));
		var filterStorage = new BlockFilterSqliteRepository(sharedSqliteStorage.GetConnectionFactory());

		Assert.True(filterStorage.TryAppend(filter0));
		Assert.True(filterStorage.TryAppend(filter1));
		Assert.True(filterStorage.TryAppend(filter2));
		Assert.True(filterStorage.TryAppend(filter3));

		bool result = filterStorage.TryRemoveLastIfNewerThan(height: 0, out FilterModel? filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(3u, filterLast.Header.Height.Height);

		result = filterStorage.TryRemoveLastIfNewerThan(height: 0, out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(2u, filterLast.Header.Height.Height);

		result = filterStorage.TryRemoveLastIfNewerThan(height: 0, out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(1u, filterLast.Header.Height.Height);

		result = filterStorage.TryRemoveLastIfNewerThan(height: 0, out filterLast);
		Assert.False(result);
		Assert.Null(filterLast);
	}

	[Fact]
	public async Task ClearAsync()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();

		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);

		var sharedSqliteStorage = SharedSqliteStorage.FromFile(Path.Combine(workDir, "Shared.sqlite"));
		var filterStorage = new BlockFilterSqliteRepository(sharedSqliteStorage.GetConnectionFactory());

		bool result = filterStorage.TryAppend(filter0);

		// Now the storage contains 2 rows.
		result = filterStorage.TryAppend(filter1);
		Assert.True(result);

		// Now we believe that the storage is empty.
		bool removedRows = filterStorage.Clear();
		Assert.True(removedRows);

		// Now the storage is empty.
		removedRows = filterStorage.Clear();
		Assert.False(removedRows);
	}

	private static FilterModel CreateFilterModel(uint blockHeight, uint256 blockHash, byte[] filterData, uint256 headerOrPrevBlockHash, long blockTime) =>
		new(
			new SmartHeader(blockHash, headerOrPrevBlockHash, blockHeight, blockTime),
			new GolombRiceFilter(filterData, 20, 1 << 20));
}
