using NBitcoin;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Backend.Models;

public record FilterModel(SmartHeader Header, GolombRiceFilter Filter)
{
	public byte[] FilterData { get; } = Filter.ToBytes();

	// https://github.com/bitcoin/bips/blob/master/bip-0158.mediawiki
	// The parameter k MUST be set to the first 16 bytes of the hash of the block for which the filter
	// is constructed.This ensures the key is deterministic while still varying from block to block.
	public byte[] FilterKey => Header.BlockHash.ToBytes()[..16];

	public static FilterModel Create(uint blockHeight, uint256 blockHash, byte[] filterData, uint256 headerOrPrevBlockHash, long blockTime) =>
		new (
			new SmartHeader(blockHash, headerOrPrevBlockHash, blockHeight, blockTime),
			new GolombRiceFilter(filterData));
}
