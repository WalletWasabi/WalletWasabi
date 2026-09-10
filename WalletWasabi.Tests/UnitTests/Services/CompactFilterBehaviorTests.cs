using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using Xunit;
namespace WalletWasabi.Tests.UnitTests.Services;

public class CompactFilterBlockHashReproTests
{
	[Fact]
	public void ValidFilterBytesAreAcceptedWithAnAttackerChosenBlockHash()
	{
		var network = Network.RegTest;
		var blockHeaderChain = new ConcurrentChain(network);
		var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = blockHeaderChain.Tip.HashBlock;
		header.Nonce = 1;
		var canonicalBlock = new ChainedBlock(header, header.GetHash(), blockHeaderChain.Tip);
		blockHeaderChain.SetTip(canonicalBlock);

		var targetScript = Script.FromHex("00141111111111111111111111111111111111111111");
		var unrelatedScript = Script.FromHex("00142222222222222222222222222222222222222222");
		var legitimateFilter = new GolombRiceFilterBuilder()
			.SetKey(canonicalBlock.HashBlock)
			.AddEntries([unrelatedScript.ToBytes()])
			.Build();
		Assert.False(legitimateFilter.MatchAny([targetScript.ToBytes()], canonicalBlock.HashBlock.ToBytes()[..16]));

		// The peer cannot change committed filter bytes, but it can freely choose the
		// unvalidated BlockHash field. This precomputed hash changes the BIP158 key and
		// turns targetScript into a false-positive match against the same filter bytes.
		var markerHash = new uint256(0x1816ful);
		Assert.NotEqual(canonicalBlock.HashBlock, markerHash);
		Assert.True(legitimateFilter.MatchAny([targetScript.ToBytes()], markerHash.ToBytes()[..16]));

		var genesisFilter = FilterCheckpoints.GetWasabiGenesisFilter(network);
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(genesisFilter.Header);
		var expectedFilterHeader = legitimateFilter.GetHeader(genesisFilter.Header.BlockFilterHeader);
		filterHeaderChain.AppendTip(
			new SmartHeader(canonicalBlock.HashBlock, expectedFilterHeader, 1, header.BlockTime));

		var synchronizationState = new FilterSynchronizationState(
			blockHeaderChain,
			filterHeaderChain,
			tipHeight: 0);
		var behavior = new CompactFilterBehavior(synchronizationState, blockHeaderChain, new EventBus());
		// Notice that the compact filter contains markerHash block hash instead of the canonical block hash. That's the attacker's choice.
		var payload = new CompactFilterPayload(FilterType.Basic, markerHash, legitimateFilter.ToBytes());

		var result = behavior.ValidateFilters(1u, [payload], network);

		Assert.Null(result);
	}
}
