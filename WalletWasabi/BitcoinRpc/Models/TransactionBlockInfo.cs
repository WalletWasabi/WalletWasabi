using NBitcoin;

namespace WalletWasabi.BitcoinRpc.Models;

public class TransactionBlockInfo
{
	public TransactionBlockInfo(uint256 blockHash, DateTimeOffset blockTime, uint blockIndex)
	{
		BlockHash = blockHash;
		BlockTime = blockTime;
		BlockIndex = blockIndex;
	}

	public uint256 BlockHash { get; }
	public DateTimeOffset BlockTime { get; }
	public uint BlockIndex { get; }
}
