using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
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

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Issue14516Async(bool copyData)
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		FilterModel filter1 = CreateFilterModel(blockHeight: 610_001, blockHash: uint256.One, filterData: DummyFilterData, headerOrPrevBlockHash: uint256.Zero, blockTime: 1231006505);

		var dataLock = new Lock();
		var hdPubKeyCache = new HdPubKeyCache();

		// Initialize
		var hdPubKey = GetHdPubKey();
		hdPubKeyCache.AddKey(hdPubKey, ScriptPubKeyType.SegwitP2SH);

		const int Iterations = 10_000;

		// Task adding keys.
		var task1 = Task.Run(() =>
		{
			for (int i = 0; i < Iterations; i++)
			{
				var hdPubKey = GetHdPubKey();

				lock (dataLock)
				{
					hdPubKeyCache.AddKey(hdPubKey, ScriptPubKeyType.SegwitP2SH);
				}
			}
		});

		// Task matching filters keys.
		var task2 = Task.Run(() =>
		{
			for (int i = 0; i < Iterations; i++)
			{
				IEnumerable<byte[]> data;

				lock (dataLock)
				{
					if (copyData)
					{
						data = hdPubKeyCache.Select(x => x.ScriptPubKeyBytes).ToArray();
					}
					else
					{
						data = hdPubKeyCache.Select(x => x.ScriptPubKeyBytes);
					}
				}

				Assert.False(filter1.Filter.MatchAny(data, filter1.FilterKey));
			}
		});

		await Task.WhenAll(task1, task2);

		static HdPubKey GetHdPubKey()
		{
			return new HdPubKey(new Key().PubKey, new KeyPath("0/0/0/0/0"), LabelsArray.Empty, KeyState.Clean);
		}
	}

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
		var filterStore = new TestableFilterStore
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

		BlockFilterIterator filterIterator = new(filterStore, maxNumberFiltersInMemory: 3);

		// Iterator needs to do a database lookup.
		{
			FilterModel? actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_001, testCts.Token);
			Assert.Equal(filter1, actualFilter);
			Assert.Equal(1, fetchCallCount);
		}

		// No database lookup is needed for block 610_001 as that one should be cached now.
		{
			FilterModel? actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_002, testCts.Token);
			Assert.Equal(filter2, actualFilter);
			Assert.Equal(1, fetchCallCount);
		}

		// No database lookup is needed for block 610_002 as that one should be cached now.
		{
			FilterModel? actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_003, testCts.Token);
			Assert.Equal(filter3, actualFilter);
			Assert.Equal(1, fetchCallCount);
		}

		// Iterator needs to do a database lookup, but now that lookup returns only 1 record and not 3 (we are close to the blockchain tip).
		{
			FilterModel? actualFilter = await filterIterator.GetAndRemoveAsync(height: 610_004, testCts.Token);
			Assert.Equal(filter4, actualFilter);
			Assert.Equal(2, fetchCallCount);
		}
	}

	private static FilterModel CreateFilterModel(uint blockHeight, uint256 blockHash, byte[] filterData, uint256 headerOrPrevBlockHash, long blockTime) =>
		new (
			new SmartHeader(blockHash, headerOrPrevBlockHash, blockHeight, blockTime),
			new GolombRiceFilter(filterData, 20, 1 << 20));
}
