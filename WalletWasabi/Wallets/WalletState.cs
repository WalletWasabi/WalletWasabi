namespace WalletWasabi.Wallets;

public enum WalletState
{
	Uninitialized,
	WaitingForInit,
	Initialized,
	Starting,
	Started,
	Stopping,
	Stopped
}
