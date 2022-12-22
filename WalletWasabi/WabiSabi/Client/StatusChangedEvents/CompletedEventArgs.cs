using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.StatusChangedEvents;

public class CompletedEventArgs : StatusChangedEventArgs
{
	public CompletedEventArgs(IWallet wallet, CompletionStatus completionStatus, CoinJoinResult coinJoinResult)
		: base(wallet)
	{
		CompletionStatus = completionStatus;
		CoinJoinResult = coinJoinResult;
	}

	public CompletionStatus CompletionStatus { get; }
	public CoinJoinResult CoinJoinResult { get; }
}
