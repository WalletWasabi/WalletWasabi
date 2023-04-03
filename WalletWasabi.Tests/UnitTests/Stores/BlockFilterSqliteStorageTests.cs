using NBitcoin;
using System.IO;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Stores;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Stores;

/// <summary>
/// Tests for <see cref="BlockFilterSqliteStorage"/>.
/// </summary>
public class BlockFilterSqliteStorageTests
{
	[Fact]
	public void TryAppend()
	{
		FilterModel startingFilter = StartingFilters.GetStartingFilter(Network.Main);

		string storagePath = $"{nameof(UnitTests)}.{nameof(BlockFilterSqliteStorage)}.{nameof(TryAppend)}.sqlite";
		File.Delete(storagePath);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(path: storagePath, startingFilter: startingFilter);

		// Starting filter for the testnet and for the mainnet are of different height. Hence, success.
		bool added = indexStorage.TryAppend(StartingFilters.GetStartingFilter(Network.TestNet));
		Assert.True(added);

		// The filter with the same block height is already present.
		added = indexStorage.TryAppend(StartingFilters.GetStartingFilter(Network.TestNet));
		Assert.False(added);
	}

	[Fact]
	public void TryRemoveLast()
	{
		FilterModel startingFilter = StartingFilters.GetStartingFilter(Network.Main);

		string storagePath = $"{nameof(UnitTests)}.{nameof(BlockFilterSqliteStorage)}.{nameof(TryRemoveLast)}.sqlite";
		File.Delete(storagePath);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(path: storagePath, startingFilter: startingFilter);

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
		byte[] dummyFilterData = Convert.FromHexString("02832810ec08a0");

		FilterModel filter0 = FilterModel.FromParameters(blockHeight: 0, blockHash: uint256.One, filterData: dummyFilterData, prevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = FilterModel.FromParameters(blockHeight: 1, blockHash: new uint256(2), filterData: dummyFilterData, prevBlockHash: uint256.One, blockTime: 1231006506);

		string storagePath = $"{nameof(UnitTests)}.{nameof(BlockFilterSqliteStorage)}.{nameof(AppendAndRemove)}.sqlite";
		File.Delete(storagePath);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(path: storagePath, startingFilter: filter0);

		bool added = indexStorage.TryAppend(filter1);
		Assert.True(added);

		bool result = indexStorage.TryRemoveLast(out FilterModel? filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.NotSame(filter1, filterLast);
		Assert.Equal(1u, filterLast.Header.Height);
		Assert.Equal(new uint256(2), filterLast.Header.BlockHash);
		Assert.Equal(uint256.One, filterLast.Header.PrevHash);
		Assert.Equal(1231006506, filterLast.Header.EpochBlockTime);
		Assert.Equal(dummyFilterData, filterLast.Filter.ToBytes());

		result = indexStorage.TryRemoveLast(out filterLast);
		Assert.True(result);
		Assert.NotNull(filterLast);
		Assert.Equal(0u, filterLast.Header.Height);
		Assert.Equal(uint256.One, filterLast.Header.BlockHash);
		Assert.Equal(uint256.Zero, filterLast.Header.PrevHash);
		Assert.Equal(1231006505, filterLast.Header.EpochBlockTime);
		Assert.Equal(dummyFilterData, filterLast.Filter.ToBytes());
	}

	[Fact]
	public void TryRemoveLastIfNewerThan()
	{
		byte[] dummyFilterData = Convert.FromHexString("02832810ec08a0");

		FilterModel filter0 = FilterModel.FromParameters(blockHeight: 0, blockHash: uint256.One, filterData: dummyFilterData, prevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = FilterModel.FromParameters(blockHeight: 1, blockHash: new uint256(2), filterData: dummyFilterData, prevBlockHash: uint256.One, blockTime: 1231006506);
		FilterModel filter2 = FilterModel.FromParameters(blockHeight: 2, blockHash: new uint256(3), filterData: dummyFilterData, prevBlockHash: new uint256(2), blockTime: 1231006507);
		FilterModel filter3 = FilterModel.FromParameters(blockHeight: 3, blockHash: new uint256(4), filterData: dummyFilterData, prevBlockHash: new uint256(3), blockTime: 1231006508);

		string storagePath = $"{nameof(UnitTests)}.{nameof(BlockFilterSqliteStorage)}.{nameof(TryRemoveLastIfNewerThan)}.sqlite";
		File.Delete(storagePath);

		using BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(path: storagePath, startingFilter: filter0);

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
		byte[] dummyFilterData = Convert.FromHexString("02832810ec08a0");

		FilterModel filter0 = FilterModel.FromParameters(blockHeight: 0, blockHash: uint256.One, filterData: dummyFilterData, prevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter1 = FilterModel.FromParameters(blockHeight: 1, blockHash: new uint256(2), filterData: dummyFilterData, prevBlockHash: uint256.One, blockTime: 1231006506);

		string storagePath = $"{nameof(UnitTests)}.{nameof(BlockFilterSqliteStorage)}.{nameof(Clear)}.sqlite";
		File.Delete(storagePath);

		using (BlockFilterSqliteStorage indexStorage = BlockFilterSqliteStorage.FromFile(path: storagePath, startingFilter: filter0))
		{
			// Now the storage contains 2 rows.
			bool result = indexStorage.TryAppend(filter1);
			Assert.True(result);

			// Now we believe that the storage is empty.
			bool removedRows = indexStorage.Clear();
			Assert.True(removedRows);
		}

		using (BlockFilterSqliteStorage indexStorage2 = BlockFilterSqliteStorage.FromFile(path: storagePath, startingFilter: null))
		{
			// Now the storage is empty.
			bool removedRows = indexStorage2.Clear();
			Assert.False(removedRows);
		}
	}
}
