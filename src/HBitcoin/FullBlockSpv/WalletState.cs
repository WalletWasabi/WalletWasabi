namespace HBitcoin.FullBlockSpv
{
	public enum WalletState
	{
		NotStarted,
		SyncingHeaders,
		SyncingBlocks,
		SyncingMemPool,
		Synced
	}
}
