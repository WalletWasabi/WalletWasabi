using NBitcoin.RPC;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.BlockFilters;

public class SyncInfo
{
	public SyncInfo(BlockchainInfo bcinfo)
	{
		Guard.NotNull(nameof(bcinfo), bcinfo);
		BlockCount = (int)bcinfo.Blocks;
		int headerCount = (int)bcinfo.Headers;
		BlockchainInfoUpdated = DateTimeOffset.UtcNow;
		IsCoreSynchronized = BlockCount == headerCount;
		InitialBlockDownload = bcinfo.InitialBlockDownload;
	}

	public int BlockCount { get; }
	public DateTimeOffset BlockchainInfoUpdated { get; }
	public bool IsCoreSynchronized { get; }

	public bool InitialBlockDownload { get; }
}
