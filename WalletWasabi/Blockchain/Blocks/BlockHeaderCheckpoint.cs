namespace WalletWasabi.Blockchain.Blocks;

/// <summary>A block header the header chain can start from, and its height.</summary>
public record BlockHeaderCheckpoint(uint Height, BlockHeader Header)
{
	public ChainedBlock ToChainedBlock() => new(Header, (int)Height);
}
