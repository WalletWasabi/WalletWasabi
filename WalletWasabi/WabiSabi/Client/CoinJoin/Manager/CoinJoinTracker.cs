using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTracker : IDisposable
{
	private bool _disposedValue;

	public CoinJoinTracker(
		Wallet wallet,
		CoinJoinClient coinJoinClient,
		Func<IEnumerable<SmartCoin>> coinCandidatesFunc,
		bool stopWhenAllMixed,
		bool overridePlebStop,
		Wallet outputWallet,
		Wallet effectiveOutputWallet,
		CancellationToken cancellationToken)
	{
		Wallet = wallet;
		_coinJoinClient = coinJoinClient;
		_coinJoinClient.CoinJoinClientProgress += CoinJoinClient_CoinJoinClientProgress;

		StopWhenAllMixed = stopWhenAllMixed;
		OverridePlebStop = overridePlebStop;
		OutputWallet = outputWallet;
		EffectiveOutputWallet = effectiveOutputWallet;
		_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CoinJoinTask = coinJoinClient.StartCoinJoinAsync(coinCandidatesFunc, _cancellationTokenSource.Token);
	}

	public event EventHandler<CoinJoinProgressEventArgs>? WalletCoinJoinProgressChanged;

	public ImmutableList<SmartCoin> CoinsInCriticalPhase => _coinJoinClient.CoinsInCriticalPhase;
	private readonly CoinJoinClient _coinJoinClient;
	private readonly CancellationTokenSource _cancellationTokenSource;

	public Wallet Wallet { get; }
	public Task<CoinJoinResult> CoinJoinTask { get; }
	public bool StopWhenAllMixed { get; set; }
	public bool OverridePlebStop { get; }
	/// <summary>Wallet the user selected as the coinjoin destination.</summary>
	/// <remarks>
	/// This is the configured destination, not necessarily where this round's outputs went. Use it to
	/// decide whether a handover is still outstanding.
	/// </remarks>
	public Wallet OutputWallet { get; }

	/// <summary>Wallet that actually received this round's outputs.</summary>
	/// <remarks>
	/// Equal to <see cref="Wallet"/> while it is still mixing towards its anonymity score target, and
	/// to <see cref="OutputWallet"/> once it starts handing over.
	/// </remarks>
	public Wallet EffectiveOutputWallet { get; }

	public bool IsCompleted => CoinJoinTask.IsCompleted;
	public bool InCriticalCoinJoinState { get; private set; }
	public bool IsStopped { get; set; }
	public List<CoinBanned> BannedCoins { get; private set; } = new();

	public void Stop()
	{
		IsStopped = true;
		if (!InCriticalCoinJoinState)
		{
			_cancellationTokenSource.Cancel();
		}
	}

	private void CoinJoinClient_CoinJoinClientProgress(object? sender, CoinJoinProgressEventArgs coinJoinProgressEventArgs)
	{
		switch (coinJoinProgressEventArgs)
		{
			case EnteringCriticalPhase:
				InCriticalCoinJoinState = true;
				break;

			case LeavingCriticalPhase:
				InCriticalCoinJoinState = false;
				break;

			case RoundEnded roundEnded:
				if (roundEnded.LastRoundState.EndRoundState != EndRoundState.TransactionBroadcasted)
				{
					Wallet.BatchedPayments.MovePaymentsToPending();
				}

				roundEnded.IsStopped = IsStopped;
				break;

			case CoinBanned coinBanned:
				BannedCoins.Add(coinBanned);
				break;
		}

		WalletCoinJoinProgressChanged?.Invoke(Wallet, coinJoinProgressEventArgs);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_coinJoinClient.CoinJoinClientProgress -= CoinJoinClient_CoinJoinClientProgress;
				_cancellationTokenSource.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
