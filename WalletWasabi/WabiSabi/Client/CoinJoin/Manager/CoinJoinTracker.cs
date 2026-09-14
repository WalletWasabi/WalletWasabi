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
		CancellationToken cancellationToken)
	{
		Wallet = wallet;
		_coinJoinClient = coinJoinClient;
		_coinJoinClient.CoinJoinClientProgress += CoinJoinClient_CoinJoinClientProgress;

		StopWhenAllMixed = stopWhenAllMixed;
		OverridePlebStop = overridePlebStop;
		OutputWallet = outputWallet;
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
	public Wallet OutputWallet { get; }

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

			case TransactionSigned transactionSigned:
				// We've signed the transaction - move payments to signed state with the txId.
				// This captures the txId immediately so we can reconcile later if the round
				// outcome is unknown.
				Wallet.BatchedPayments.MovePaymentsToSigned(transactionSigned.TransactionId);
				break;

			case RoundEnded roundEnded:
				var endState = roundEnded.LastRoundState.EndRoundState;
				// Only reset payments if we KNOW the round failed.
				// When EndRoundState is None (unknown), the transaction might have been
				// broadcast, so we must NOT move payments back to pending to avoid double payments.
				// Payments in Signed state will be resolved by reconciliation or timeout.
				if (endState != EndRoundState.TransactionBroadcasted &&
					endState != EndRoundState.None)
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
