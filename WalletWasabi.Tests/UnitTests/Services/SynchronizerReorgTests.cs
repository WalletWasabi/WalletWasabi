using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

/// <summary>
/// Two blocks at the same height: filters keep the loser, headers keep the winner.
/// Sync must return <see cref="FiltersResponse.BestBlockUnknown"/> so the store
/// drops the loser and can follow the winner.
/// </summary>
public class SynchronizerReorgTests(ITestOutputHelper output)
{
	[Fact]
	public async Task P2pProvider_HeaderChainBehindFilterCheckpoint_WaitsForHeaderChainToCatchUp()
	{
		var checkpoint = FilterCheckpoints.GetCheckpointForBirthday(700_000u, Network.Main);
		var blockHeaders = new ConcurrentChain(Network.Main);
		var filterHeaders = new FilterHeaderChain();
		filterHeaders.AppendTip(checkpoint.Header);
		var synchronizationState = new FilterSynchronizationState(blockHeaders, filterHeaders, checkpoint.Header.Height);

		var filterProvider = FilterProviders.CreateBitcoinP2pFilterProvider(filterHeaders, blockHeaders, synchronizationState);
		var result = await filterProvider(checkpoint.Header.Height, checkpoint.Header.BlockHash, TestContext.Current.CancellationToken);

		Proof($"P2P checkpoint={checkpoint.Header.BlockHash} filterHeight={checkpoint.Header.Height} headerHeight={blockHeaders.Tip.Height} got {Describe(result)} want AlreadyOnBestBlock");
		Assert.False(result.IsOk);
		Assert.Equal(FilterProviders.WaitForBlockHeadersToCatchUp, result.Error);
	}

	[Fact]
	public async Task RpcProvider_OrphanedFilterTip_ReturnsBestBlockUnknown()
	{
		var fork = Fork.Create();
		Proof($"RPC  filters={fork.Orphan.ToString()[..8]}…  headers={fork.Winner.ToString()[..8]}…  height={fork.Height}");

		var result = await FilterProviders
			.CreateBitcoinRpcFilterProvider(new MockRpcClient(), fork.BlockHeaders)
			(fork.Height, fork.Orphan, CancellationToken.None);

		Proof($"RPC  got {Describe(result)}  want BestBlockUnknown");
		AssertBestBlockUnknown(result);
	}

	[Fact]
	public async Task P2pProvider_OrphanedFilterTip_ReturnsBestBlockUnknown()
	{
		var fork = Fork.Create();
		Proof($"P2P  filters={fork.Orphan.ToString()[..8]}…  headers={fork.Winner.ToString()[..8]}…  height={fork.Height}");

		var result = await FilterProviders
			.CreateBitcoinP2pFilterProvider(fork.FiltersOnOrphan, fork.BlockHeaders, fork.SyncState)
			(fork.Height, fork.Orphan, CancellationToken.None);

		Proof($"P2P  got {Describe(result)}  want BestBlockUnknown");
		AssertBestBlockUnknown(result);
	}

	[Fact]
	public void HeaderAssignment_AfterTipRollback_StartsAtReorgedHeight()
	{
		var fork = Fork.Create();
		Assert.True(fork.SyncState.IsReorg(fork.Height, fork.Orphan));

		// Store dropped the orphan. Next header request must re-fetch that height, not skip it.
		fork.FiltersOnOrphan.RemoveTip();

		var assigned = fork.SyncState.TryAssignHeaderRange(Network.RegTest, out var next);
		Proof($"next request start={next?.StartHeight} stop={next?.StopHash.ToString()[..8]}…  want height={fork.Height} winner={fork.Winner.ToString()[..8]}…");

		Assert.True(assigned);
		Assert.Equal(fork.Height, next!.StartHeight);
		Assert.Equal(fork.Winner, next.StopHash);
	}

	/// <summary>
	/// <code>
	///            orphan (filters still here)
	///           /
	///  0 ── 1 ─┤
	///           \
	///            winner (headers already here)
	/// </code>
	/// Same height, different hashes. Asking "filters after orphan" finds nothing.
	/// </summary>
	private sealed class Fork
	{
		public required ConcurrentChain BlockHeaders { get; init; }
		public required FilterHeaderChain FiltersOnOrphan { get; init; }
		public required FilterSynchronizationState SyncState { get; init; }
		public required uint Height { get; init; }
		public required uint256 Orphan { get; init; }
		public required uint256 Winner { get; init; }

		public static Fork Create()
		{
			var blockHeaders = new ConcurrentChain(Network.RegTest);
			Mine(blockHeaders);
			Mine(blockHeaders);

			var orphan = blockHeaders.Tip.HashBlock;
			var height = (uint)blockHeaders.Tip.Height;

			var winnerHeader = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
			winnerHeader.HashPrevBlock = blockHeaders.Tip.Previous.HashBlock;
			winnerHeader.Nonce = 1;
			blockHeaders.SetTip(new ChainedBlock(winnerHeader, winnerHeader.GetHash(), blockHeaders.Tip.Previous));

			var filters = new FilterHeaderChain();
			for (var h = 0; h <= blockHeaders.Tip.Height; h++)
			{
				var hash = h == (int)height ? orphan : blockHeaders.GetBlock(h).HashBlock;
				filters.AppendTip(new SmartHeader(hash, new uint256((ulong)(h + 1)), (uint)h, DateTimeOffset.UtcNow));
			}

			return new Fork
			{
				BlockHeaders = blockHeaders,
				FiltersOnOrphan = filters,
				SyncState = new FilterSynchronizationState(blockHeaders, filters, height),
				Height = height,
				Orphan = orphan,
				Winner = blockHeaders.Tip.HashBlock
			};
		}

		private static void Mine(ConcurrentChain chain)
		{
			var header = Network.RegTest.Consensus.ConsensusFactory.CreateBlockHeader();
			header.HashPrevBlock = chain.Tip.HashBlock;
			chain.SetTip(new ChainedBlock(header, header.GetHash(), chain.Tip));
		}
	}

	private static void AssertBestBlockUnknown(Result<FiltersResponse, TimeSpan> result)
	{
		Assert.True(result.IsOk);
		Assert.IsType<FiltersResponse.BestBlockUnknown>(result.Value);
	}

	private void Proof(string line)
	{
		Logger.LogInfo($"PROOF {line}");
		output.WriteLine($"PROOF {line}");
	}

	private static string Describe(Result<FiltersResponse, TimeSpan> result) =>
		result.IsOk ? result.Value.GetType().Name : $"retry {result.Error.TotalSeconds}s";
}
