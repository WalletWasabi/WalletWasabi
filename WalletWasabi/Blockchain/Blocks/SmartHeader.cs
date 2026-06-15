using NBitcoin;

namespace WalletWasabi.Blockchain.Blocks;

public record SmartHeader
{
	public SmartHeader(uint256 blockHash, uint256 header, uint height, DateTimeOffset blockTime)
		: this(blockHash, header, height, blockTime.ToUnixTimeSeconds())
	{
	}

	public SmartHeader(uint256 blockHash, uint256 header, uint height, long epochBlockTime)
	{
		BlockHash = blockHash;
		BlockFilterHeader = header;
		Height = new ChainHeight(height);
		EpochBlockTime = epochBlockTime;
	}

	public uint256 BlockHash { get; }
	public uint256 BlockFilterHeader { get; }
	public ChainHeight Height { get; }
	public long EpochBlockTime { get; }
	public DateTimeOffset BlockTime => DateTimeOffset.FromUnixTimeSeconds(EpochBlockTime);
}
