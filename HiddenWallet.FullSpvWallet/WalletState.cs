namespace HiddenWallet.FullSpv
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
