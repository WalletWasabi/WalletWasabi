using NBitcoin;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class FilterSynchronizationStateTests
{
	private static readonly Network Network = Network.RegTest;

	[Fact]
	public void AFilterRangeStaysAssignedUntilTheNodeReleasesIt()
	{
		var state = CreateState(blockHeaders: 1000, filterHeaders: 1000);

		Assert.True(state.TryAssignFilterRange(out var first));
		Assert.True(state.TryAssignFilterRange(out var second));
		Assert.NotEqual(first, second);
		Assert.False(state.TryAssignFilterRange(out _));

		state.OnFilterNodeDisconnected(first);

		Assert.True(state.TryAssignFilterRange(out var reassigned));
		Assert.Equal(first, reassigned);
	}

	[Fact]
	public void AHeaderRangeStaysAssignedUntilTheNodeReleasesIt()
	{
		var state = CreateState(blockHeaders: 4000, filterHeaders: 0);

		Assert.True(state.TryAssignHeaderRange(Network, out var first));
		Assert.True(state.TryAssignHeaderRange(Network, out var second));
		Assert.NotEqual(first, second);
		Assert.False(state.TryAssignHeaderRange(Network, out _));

		state.OnHeaderNodeDisconnected(first);

		Assert.True(state.TryAssignHeaderRange(Network, out var reassigned));
		Assert.Equal(first, reassigned);
	}

	private static FilterSynchronizationState CreateState(int blockHeaders, int filterHeaders)
	{
		var blockHeaderChain = new ConcurrentChain(Network);
		var tip = blockHeaderChain.Tip;
		for (var i = 0; i < blockHeaders; i++)
		{
			var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.HashPrevBlock = tip.HashBlock;
			header.Nonce = (uint)(tip.Height + 1);
			tip = new ChainedBlock(header, header.GetHash(), tip);
		}

		blockHeaderChain.SetTip(tip);

		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetWasabiGenesisFilter(Network).Header);
		for (var height = 1u; height <= filterHeaders; height++)
		{
			filterHeaderChain.AppendTip(new SmartHeader(blockHeaderChain.GetBlock((int)height).HashBlock, new uint256(height), height, 0L));
		}

		return new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0);
	}
}
