using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class WalletCoinJoinState
{
	public static bool IsUserInSendWorkflow { get; set; }
	private static TimeSpan AutoCoinJoinDelayAfterWalletLoaded { get; } = TimeSpan.FromMinutes(Random.Shared.Next(5, 16));

	public WalletCoinJoinState(Wallet wallet)
	{
		Wallet = wallet;
	}

	public event EventHandler<WalletStatusChangedEventArgs>? WalletStatusChanged;

	public Wallet Wallet { get; }
	private CoinJoinTracker? CoinJoinTracker { get; set; }
	public bool OverrideAutoCoinJoinStartTime { get; set; }

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

	public DateTimeOffset AutoCoinJoinStartTime
	{
		get
		{
			if (Wallet.State < WalletState.Started)
			{
				throw new InvalidOperationException("Wallet is not started yet.");
			}
			return Wallet.StartupTime + AutoCoinJoinDelayAfterWalletLoaded;
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

	public bool CanStartAutoCoinJoin
	{
		get
		{
			if (!Wallet.KeyManager.AutoCoinJoin)
			{
				return false;
			}

			if (IsUserInSendWorkflow)
			{
				return false;
			}

			if (!OverrideAutoCoinJoinStartTime && Wallet.ElapsedTimeSinceStartup <= AutoCoinJoinDelayAfterWalletLoaded)
			{
				return false;
			}

			if (Wallet.NonPrivateCoins.TotalAmount() <= Wallet.KeyManager.PlebStopThreshold)
			{
				return false;
			}

			return true;
		}
	}
}
