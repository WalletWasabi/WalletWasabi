using NBitcoin;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Stores;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Stores;

/// <summary>
/// Tests for <see cref="BlockFilterSqliteStorage"/>.
/// </summary>
public class BlockFilterSqliteStorageTests
{
	private static byte[] DummyFilterData = Convert.FromHexString("02832810ec08a0");

	[Fact]
	public void TryAppend()
	{
		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(dataSource: SqliteStorageHelper.InMemoryDatabase, filter0);

		bool added = indexStorage.TryAppend(filter1);
		Assert.True(added);

		// The filter with the same block height is already present.
		added = indexStorage.TryAppend(filter1);
		Assert.False(added);
	}

	[Fact]
	public void TryRemoveLast()
	{
		FilterModel startingFilter = StartingFilters.GetStartingFilter(Network.Main);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(dataSource: SqliteStorageHelper.InMemoryDatabase, startingFilter);

		bool result = indexStorage.TryRemoveLast(out FilterModel? filter1);
		Assert.True(result);
		Assert.NotNull(filter1);
		Assert.NotSame(startingFilter, filter1); // The filter was stored in the database and removed from the database, so no reference equality.

		result = indexStorage.TryRemoveLast(out FilterModel? filter2);
		Assert.False(result);
		Assert.Null(filter2);
	}

	[Fact]
	public void AppendAndRemove()
	{
		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(dataSource: SqliteStorageHelper.InMemoryDatabase, filter0);

		bool added = indexStorage.TryAppend(filter1);
		Assert.True(added);

		bool result = indexStorage.TryRemoveLast(out FilterModel? filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.NotSame(filter1, filterLast);
		Assert.Equal(1u, filterLast.Header.Height);
		Assert.Equal(new uint256(2), filterLast.Header.BlockHash);
		Assert.Equal(uint256.One, filterLast.Header.HeaderOrPrevBlockHash);
		Assert.Equal(1231006506, filterLast.Header.EpochBlockTime);
		Assert.Equal(DummyFilterData, filterLast.Filter.ToBytes());

		result = indexStorage.TryRemoveLast(out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(0u, filterLast.Header.Height);
		Assert.Equal(uint256.One, filterLast.Header.BlockHash);
		Assert.Equal(uint256.Zero, filterLast.Header.HeaderOrPrevBlockHash);
		Assert.Equal(1231006505, filterLast.Header.EpochBlockTime);
		Assert.Equal(DummyFilterData, filterLast.Filter.ToBytes());
	}

	[Fact]
	public void TryRemoveLastIfNewerThan()
	{
		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);
		FilterModel filter2 = CreateFilterModel(blockHeight: 2, blockHash: new uint256(3), filterData: DummyFilterData, headerOrPrevBlockHash: new uint256(2), blockTime: 1231006507);
		FilterModel filter3 = CreateFilterModel(blockHeight: 3, blockHash: new uint256(4), filterData: DummyFilterData, headerOrPrevBlockHash: new uint256(3), blockTime: 1231006508);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(dataSource: SqliteStorageHelper.InMemoryDatabase, filter0);

		Assert.True(indexStorage.TryAppend(filter1));
		Assert.True(indexStorage.TryAppend(filter2));
		Assert.True(indexStorage.TryAppend(filter3));

		bool result = indexStorage.TryRemoveLastIfNewerThan(height: 0, out FilterModel? filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(3u, filterLast.Header.Height);

		result = indexStorage.TryRemoveLastIfNewerThan(height: 0, out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(2u, filterLast.Header.Height);

		result = indexStorage.TryRemoveLastIfNewerThan(height: 0, out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(1u, filterLast.Header.Height);

		result = indexStorage.TryRemoveLastIfNewerThan(height: 0, out filterLast);
		Assert.False(result);
		Assert.Null(filterLast);
	}

	[Fact]
	public void Clear()
	{
		FilterModel filter0 = CreateFilterModel(blockHeight: 0, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = CreateFilterModel(blockHeight: 1, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(dataSource: SqliteStorageHelper.InMemoryDatabase, filter0);

		// Now the storage contains 2 rows.
		bool result = indexStorage.TryAppend(filter1);
		Assert.True(result);

		// Now we believe that the storage is empty.
		bool removedRows = indexStorage.Clear();
		Assert.True(removedRows);

		// Now the storage is empty.
		removedRows = indexStorage.Clear();
		Assert.False(removedRows);
	}

	private static FilterModel CreateFilterModel(uint blockHeight, uint256 blockHash, byte[] filterData, uint256 headerOrPrevBlockHash, long blockTime) =>
		new (
			new SmartHeader(blockHash, headerOrPrevBlockHash, blockHeight, blockTime),
			new GolombRiceFilter(filterData, 20, 1 << 20));
}
