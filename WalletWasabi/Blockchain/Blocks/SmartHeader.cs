namespace WalletWasabi.Blockchain.Blocks;

public record SmartHeader
{
	public SmartHeader(uint256 blockHash, uint256 blockFilterHeader, uint height, DateTimeOffset blockTime)
		: this(blockHash, blockFilterHeader, height, blockTime.ToUnixTimeSeconds())
	{
	}

	public SmartHeader(uint256 blockHash, uint256 blockFilterHeader, uint height, long epochBlockTime)
	{
		BlockHash = blockHash;
		BlockFilterHeader = blockFilterHeader;
		Height = new ChainHeight(height);
		EpochBlockTime = epochBlockTime;
	}

	public uint256 BlockHash { get; }
	public uint256 BlockFilterHeader { get; }
	public ChainHeight Height { get; }
	public long EpochBlockTime { get; }
	public DateTimeOffset BlockTime => DateTimeOffset.FromUnixTimeSeconds(EpochBlockTime);
}
