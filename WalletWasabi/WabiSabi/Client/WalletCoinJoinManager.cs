using WalletWasabi.WabiSabi.Client.CoinJoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class WalletCoinJoinManager
{
	private IWalletCoinJoinState _walletCoinJoinState;

	public static bool IsUserInSendWorkflow { get; set; }
	private static TimeSpan AutoCoinJoinDelayAfterWalletLoaded { get; } = TimeSpan.FromMinutes(Random.Shared.Next(5, 16));

	public WalletCoinJoinManager(Wallet wallet)
	{
		Wallet = wallet;
		_walletCoinJoinState = new Stopped();
	}

	public event EventHandler<IWalletCoinJoinState>? StateChanged;

	public Wallet Wallet { get; }
	private CoinJoinTracker? CoinJoinTracker { get; set; }
	private bool OverrideAutoCoinJoinDelay { get; set; }
	private bool IsPaused { get; set; }
	private bool IsPlaying { get; set; }

	private bool IsDelay => !OverrideAutoCoinJoinDelay && Wallet.ElapsedTimeSinceStartup <= AutoCoinJoinDelayAfterWalletLoaded;
	private bool IsPlebStop => Wallet.NonPrivateCoins is { } coins && coins.TotalAmount() <= Wallet.KeyManager.PlebStopThreshold;

	public bool AutoCoinJoin => Wallet.KeyManager.AutoCoinJoin;

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

	public IWalletCoinJoinState WalletCoinJoinState
	{
		get => _walletCoinJoinState;
		private set
		{
			if (_walletCoinJoinState != value)
			{
				_walletCoinJoinState = value;
				StateChanged?.Invoke(this, value);
			}
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
	}

	internal void ClearCoinJoinTracker()
	{
		CoinJoinTracker = null;
	}

	public void Play()
	{
		OverrideAutoCoinJoinDelay = true;
		IsPaused = false;
		IsPlaying = true;
	}

	public void Pause()
	{
		IsPaused = true;
	}

	public void Stop()
	{
		IsPaused = false;
		IsPlaying = false;
	}

	public void Step()
	{
		switch (WalletCoinJoinState)
		{
			case Stopped:
				if (AutoCoinJoin is true)
				{
					WalletCoinJoinState = new AutoStarting();
					return;
				}
				if (!IsPlaying)
				{
					return;
				}

				WalletCoinJoinState = new Playing();
				break;

			case AutoStarting:
				if (AutoCoinJoin is false)
				{
					WalletCoinJoinState = new Stopped();
					return;
				}

				// Can we start automatic CoinJoin?
				if (IsUserInSendWorkflow || IsPaused || IsDelay || IsPlebStop)
				{
					WalletCoinJoinState = new AutoStarting(
						IsPlebStop: IsPlebStop,
						IsPaused: IsPaused,
						IsDelay: IsDelay,
						IsSending: IsUserInSendWorkflow);
					return;
				}

				WalletCoinJoinState = new Playing();
				break;

			case Playing state:

				if (!IsPlaying && CoinJoinTracker?.InCriticalCoinJoinState is not true)
				{
					WalletCoinJoinState = new Stopped();
					return;
				}

				if (!state.IsInRound && CoinJoinTracker is { } cjt)
				{
					WalletCoinJoinState = new Playing(IsInRound: true, InCriticalPhase: cjt.InCriticalCoinJoinState);
					return;
				}

				if (state.IsInRound && CoinJoinTracker is null)
				{
					WalletCoinJoinState = new Finished();
					return;
				}
				break;

			case Finished:
				WalletCoinJoinState = AutoCoinJoin ? new AutoStarting() : new Stopped();
				break;

			default:
				WalletCoinJoinState = new Stopped();
				break;
		}
	}
}
