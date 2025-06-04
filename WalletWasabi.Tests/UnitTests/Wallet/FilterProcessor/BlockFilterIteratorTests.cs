using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;
using ArgumentException = System.ArgumentException;

namespace WalletWasabi.Tests.UnitTests.Wallet.FilterProcessor;

/// <summary>
/// Tests for <see cref="BlockFilterIterator"/>.
/// </summary>
public class BlockFilterIteratorTests
{
	private static byte[] DummyFilterData = Convert.FromHexString("02832810ec08a0");

	/// <summary>
	/// Verifies that the iterator behaves as expected with respect to iterating one block filter after another.
	/// </summary>
	[Fact]
	public async Task GetAndRemoveTestAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		FilterModel filter1 = CreateFilterModel(blockHeight: 610_001, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter2 = CreateFilterModel(blockHeight: 610_002, blockHash: new uint256(2), filterData: DummyFilterData, headerOrPrevBlockHash: uint256.One, blockTime: 1231006506);
		FilterModel filter3 = CreateFilterModel(blockHeight: 610_003, blockHash: new uint256(3), filterData: DummyFilterData, headerOrPrevBlockHash: new uint256(2), blockTime: 1231006506);
		FilterModel filter4 = CreateFilterModel(blockHeight: 610_004, blockHash: new uint256(4), filterData: DummyFilterData, headerOrPrevBlockHash: new uint256(3), blockTime: 1231006506);

		var fetchCallCount = 0;
		var indexStore = new TesteableIndexStore
		{
			OnFetchBatchAsync = (fromHeight, count, _) =>
			{
				fetchCallCount++;
				return Task.FromResult((fromHeight, count) switch
				{
					(610_001, 3) => new[] {filter1, filter2, filter3},
					(610_004, 3) => new[] {filter4},
					_ => throw new ArgumentException()
				});
			}
		};

		BlockFilterIterator filterIterator = new(indexStore, maxNumberFiltersInMemory: 3);

		// Iterator needs to do a database lookup.
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_001, testCts.Token);
			Assert.Equal(filter1, actualFilter);
			Assert.Equal(1, fetchCallCount);
		}

		// No database lookup is needed for block 610_001 as that one should be cached now.
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_002, testCts.Token);
			Assert.Equal(filter2, actualFilter);
			Assert.Equal(1, fetchCallCount);
		}

		// No database lookup is needed for block 610_002 as that one should be cached now.
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_003, testCts.Token);
			Assert.Equal(filter3, actualFilter);
			Assert.Equal(1, fetchCallCount);
		}

		// Iterator needs to do a database lookup, but now that lookup returns only 1 record and not 3 (we are close to the blockchain tip).
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_004, testCts.Token);
			Assert.Equal(filter4, actualFilter);
			Assert.Equal(2, fetchCallCount);
		}
	}

	private static FilterModel CreateFilterModel(uint blockHeight, uint256 blockHash, byte[] filterData, uint256 headerOrPrevBlockHash, long blockTime) =>
		new (
			new SmartHeader(blockHash, headerOrPrevBlockHash, blockHeight, blockTime),
			new GolombRiceFilter(filterData, 20, 1 << 20));
}
