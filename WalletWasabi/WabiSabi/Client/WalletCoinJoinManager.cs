using WalletWasabi.WabiSabi.Client.CoinJoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class WalletCoinJoinManager
{
	private WalletCoinjoinState _walletCoinJoinState;

	public static bool IsUserInSendWorkflow { get; set; }
	private static TimeSpan AutoCoinJoinDelayAfterWalletLoaded { get; } = TimeSpan.FromMinutes(Random.Shared.Next(5, 16));

	public WalletCoinJoinManager(Wallet wallet)
	{
		Wallet = wallet;
		_walletCoinJoinState = GetZeroState();
	}

	public event EventHandler<WalletCoinjoinState>? StateChanged;

	public Wallet Wallet { get; }
	private CoinJoinTracker? CoinJoinTracker { get; set; }
	private bool OverrideAutoCoinJoinDelay { get; set; }
	private bool IsPaused { get; set; }
	private bool IsPlaying { get; set; }

	private bool IsDelay => !OverrideAutoCoinJoinDelay && (Wallet.State < WalletState.Started || Wallet.ElapsedTimeSinceStartup <= AutoCoinJoinDelayAfterWalletLoaded);
	private bool IsPlebStop => Wallet.NonPrivateCoins is { } coins && coins.TotalAmount() <= Wallet.KeyManager.PlebStopThreshold;

	public bool AutoCoinJoin => Wallet.KeyManager.AutoCoinJoin;

	public WalletCoinjoinState WalletCoinjoinState
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
		IsPlaying = false;
		OverrideAutoCoinJoinDelay = false;
	}

	public void Stop()
	{
		IsPaused = false;
		IsPlaying = false;
		OverrideAutoCoinJoinDelay = false;
	}

	public void UpdateState()
	{
		var state = WalletCoinjoinState;
		switch (state.Status)
		{
			case WalletCoinjoinState.State.Stopped:
				if (AutoCoinJoin is true)
				{
					WalletCoinjoinState = WalletCoinjoinState.AutoStarting();
					return;
				}
				if (!IsPlaying)
				{
					return;
				}

				WalletCoinjoinState = WalletCoinjoinState.Playing();
				break;

			case WalletCoinjoinState.State.AutoStarting:
				if (AutoCoinJoin is false)
				{
					WalletCoinjoinState = GetZeroState();
					return;
				}

				if (IsUserInSendWorkflow || IsPaused || IsDelay || IsPlebStop)
				{
					WalletCoinjoinState = WalletCoinjoinState.AutoStarting(
						isPlebStop: IsPlebStop,
						isPaused: IsPaused,
						isDelay: IsDelay,
						isSending: IsUserInSendWorkflow);
					return;
				}

				WalletCoinjoinState = WalletCoinjoinState.Playing();
				break;

			case WalletCoinjoinState.State.Playing:
				if ((!IsPlaying || IsPaused) && CoinJoinTracker?.InCriticalCoinJoinState is not true)
				{
					WalletCoinjoinState = WalletCoinjoinState.Stopped();
					return;
				}

				if (!state.InRound && CoinJoinTracker is { } cjt)
				{
					WalletCoinjoinState = WalletCoinjoinState.Playing(inRound: true, inCriticalPhase: cjt.InCriticalCoinJoinState);
					return;
				}

				if (state.InRound && CoinJoinTracker is null)
				{
					WalletCoinjoinState = WalletCoinjoinState.Finished();
					return;
				}
				break;

			case WalletCoinjoinState.State.Finished:
				WalletCoinjoinState = GetZeroState();
				break;

			default:
				WalletCoinjoinState = GetZeroState();
				break;
		}
	}

	private WalletCoinjoinState GetZeroState()
	{
		if (AutoCoinJoin)
		{
			return WalletCoinjoinState.AutoStarting(IsUserInSendWorkflow, IsPlebStop, IsDelay, IsPaused);
		}

		return IsPlaying ? WalletCoinjoinState.Playing() : WalletCoinjoinState.Stopped();
	}
}
