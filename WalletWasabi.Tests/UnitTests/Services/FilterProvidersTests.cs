using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class FilterProvidersTests
{
	private static readonly byte[] DummyFilterData = Convert.FromHexString("02832810ec08a0");

	[Fact]
	public async Task BitcoinRpcProviderFetchesBoundedPageAndKeepsBestHeightAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		var blockHashes = Enumerable.Range(1, 250)
			.ToDictionary(x => x, x => new uint256((ulong)x));
		List<int> requestedBlockHeights = [];
		List<uint256> requestedFilterHashes = [];

		MockRpcClient rpc = new()
		{
			Network = Network.Main,
			OnGetBlockCountAsync = () => Task.FromResult(250),
			OnGetBlockHashAsync = height =>
			{
				requestedBlockHeights.Add(height);
				return Task.FromResult(blockHashes[height]);
			},
			OnGetBlockFilterAsync = blockHash =>
			{
				requestedFilterHashes.Add(blockHash);
				return Task.FromResult(CreateBlockFilter(blockHash));
			}
		};

		var genesisHash = Network.Main.GetGenesis().GetHash();
		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpc, new ConcurrentChain(Network.Main));
		var result = await provider(fromHeight: 0, fromHash: genesisHash, testCts.Token);

		Assert.True(result.IsOk);
		var newFilters = Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		Assert.Equal(250u, newFilters.BestHeight.Height);
		Assert.Equal(100, newFilters.Filters.Length);
		Assert.Equal(Enumerable.Range(1, 100), requestedBlockHeights);
		Assert.Equal(Enumerable.Range(1, 100).Select(x => blockHashes[x]), requestedFilterHashes);
		Assert.Equal(1u, newFilters.Filters[0].Header.Height.Height);
		Assert.Equal(100u, newFilters.Filters[^1].Header.Height.Height);
	}

	[Fact]
	public async Task BitcoinRpcProviderHandlesPartialBlockHashFailures()
	{
		var blockHashes = Enumerable.Range(1, 100)
			.ToDictionary(x => x, x => new uint256((ulong)x));

		MockRpcClient rpc = new()
		{
			Network = Network.Main,
			OnGetBlockCountAsync = () => Task.FromResult(100),
			OnGetBlockHashAsync = height =>
			{
				// Simulate reorg: blocks 51-100 no longer exist
				if (height > 50)
				{
					return Task.FromException<uint256>(
						new RPCException(RPCErrorCode.RPC_INVALID_PARAMETER, "Block height out of range", null));
				}
				return Task.FromResult(blockHashes[height]);
			},
			OnGetBlockFilterAsync = blockHash => Task.FromResult(CreateBlockFilter(blockHash))
		};

		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		var genesisHash = Network.Main.GetGenesis().GetHash();
		var provider = FilterProviders.CreateBitcoinRpcFilterProvider(rpc, new ConcurrentChain(Network.Main));
		var result = await provider(fromHeight: 0, fromHash: genesisHash, testCts.Token);

		Assert.True(result.IsOk);
		var newFilters = Assert.IsType<FiltersResponse.NewFiltersAvailable>(result.Value);
		// Should only get 50 filters (the ones that succeeded)
		Assert.Equal(50, newFilters.Filters.Length);
		Assert.Equal(1u, newFilters.Filters[0].Header.Height.Height);
		Assert.Equal(50u, newFilters.Filters[^1].Header.Height.Height);
	}

	private static BlockFilter CreateBlockFilter(uint256 blockHash) =>
		new(new GolombRiceFilter(DummyFilterData, 20, 1 << 20), blockHash);
}
