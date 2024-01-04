using Moq;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Stores;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;

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

		FilterModel filter1 = FilterModel.Create(blockHeight: 610_001, blockHash: uint256.One, filterData: DummyFilterData, prevBlockHash: uint256.Zero, blockTime: 1231006505);
		FilterModel filter2 = FilterModel.Create(blockHeight: 610_002, blockHash: new uint256(2), filterData: DummyFilterData, prevBlockHash: uint256.One, blockTime: 1231006506);
		FilterModel filter3 = FilterModel.Create(blockHeight: 610_003, blockHash: new uint256(3), filterData: DummyFilterData, prevBlockHash: new uint256(2), blockTime: 1231006506);
		FilterModel filter4 = FilterModel.Create(blockHeight: 610_004, blockHash: new uint256(4), filterData: DummyFilterData, prevBlockHash: new uint256(3), blockTime: 1231006506);

		Mock<IIndexStore> mockIndexStore = new(MockBehavior.Strict);
		_ = mockIndexStore.Setup(c => c.FetchBatchAsync(610_001, 3, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new FilterModel[] { filter1, filter2, filter3 });

		BlockFilterIterator filterIterator = new(mockIndexStore.Object, maxNumberFiltersInMemory: 3);

		// Iterator needs to do a database lookup.
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_001, testCts.Token);
			Assert.Equal(filter1, actualFilter);

			// The internal cache should not contain the filter anymore.
			Assert.False(filterIterator.Cache.ContainsKey(610_001));

			mockIndexStore.Verify(c => c.FetchBatchAsync(610_001, 3, It.IsAny<CancellationToken>()));
		}

		// No database lookup is needed for block 610_001 as that one should be cached now.
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_002, testCts.Token);
			Assert.Equal(filter2, actualFilter);
			Assert.False(filterIterator.Cache.ContainsKey(610_002));
		}

		// No database lookup is needed for block 610_002 as that one should be cached now.
		{
			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_003, testCts.Token);
			Assert.Equal(filter3, actualFilter);
			Assert.False(filterIterator.Cache.ContainsKey(610_003));

			Assert.Empty(filterIterator.Cache);
		}

		// Iterator needs to do a database lookup, but now that lookup returns only 1 record and not 3 (we are close to the blockchain tip).
		{
			_ = mockIndexStore.Setup(c => c.FetchBatchAsync(610_004, 3, It.IsAny<CancellationToken>()))
				.ReturnsAsync(new FilterModel[] { filter4 });

			FilterModel actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_004, testCts.Token);
			Assert.Equal(filter4, actualFilter);
			Assert.False(filterIterator.Cache.ContainsKey(610_004));

			// Cache is empty again because we have got only 1 record from the index store.
			Assert.Empty(filterIterator.Cache);

			mockIndexStore.Verify(c => c.FetchBatchAsync(610_004, 3, It.IsAny<CancellationToken>()));
		}

		mockIndexStore.VerifyAll();
	}
}
