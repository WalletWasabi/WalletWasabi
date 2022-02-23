using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class WalletCoinJoinState
{
	public static bool IsUserInSendWorkflow { get; set; }

	public WalletCoinJoinState(Wallet wallet)
	{
		Wallet = wallet;
	}

	public event EventHandler<WalletStatusChangedEventArgs>? WalletStatusChanged;

	public Wallet Wallet { get; }
	private CoinJoinTracker? CoinJoinTracker { get; set; }

	public CoinJoinClientState CoinJoinClientState
	{
		get
		{
			if (CoinJoinTracker is not { } coinJoinTracker || coinJoinTracker.IsCompleted)
			{
				return CoinJoinClientState.Idle;
			}

			return coinJoinTracker.InCriticalCoinJoinState
				? CoinJoinClientState.InCriticalPhase
				: CoinJoinClientState.InProgress;
		}
	}

	internal void SetCoinJoinTracker(CoinJoinTracker coinJoinTracker)
	{
		CoinJoinTracker = coinJoinTracker;
		WalletStatusChanged?.Invoke(this, new WalletStatusChangedEventArgs(Wallet, IsCoinJoining: true));
	}

	internal void ClearCoinJoinTracker()
	{
		CoinJoinTracker = null;
		WalletStatusChanged?.Invoke(this, new WalletStatusChangedEventArgs(Wallet, IsCoinJoining: false));
	}
}
