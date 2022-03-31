using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTracker : IDisposable
{
	private bool _disposedValue;

	public CoinJoinTracker(
		Wallet wallet,
		CoinJoinClient coinJoinClient,
		IEnumerable<SmartCoin> coinCandidates,
		bool restartAutomatically,
		CancellationToken cancellationToken)
	{
		Wallet = wallet;
		CoinJoinClient = coinJoinClient;
		CoinCandidates = coinCandidates;
		RestartAutomatically = restartAutomatically;
		CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CoinJoinClient.CoinJoinClientProgress += CoinJoinClient_CoinJoinClientProgress;
		CoinJoinTask = coinJoinClient.StartCoinJoinAsync(coinCandidates, CancellationTokenSource.Token);
	}

	private CoinJoinClient CoinJoinClient { get; }
	private CancellationTokenSource CancellationTokenSource { get; }

	public Wallet Wallet { get; }
	public Task<CoinJoinResult> CoinJoinTask { get; }
	public IEnumerable<SmartCoin> CoinCandidates { get; }
	public bool RestartAutomatically { get; }

	public bool IsCompleted => CoinJoinTask.IsCompleted;
	public bool InCriticalCoinJoinState { get; private set; }

	public void Cancel()
	{
		CancellationTokenSource.Cancel();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				CancellationTokenSource.Dispose();
				CoinJoinClient.CoinJoinClientProgress -= CoinJoinClient_CoinJoinClientProgress;
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private void CoinJoinClient_CoinJoinClientProgress(object? sender, CoinJoinProgressEventArgs e) =>
		InCriticalCoinJoinState = e switch
		{
			CoinJoinEnteringInCriticalPhaseEventArgs => true,
			CoinJoinLeavingCriticalPhaseEventArgs => false,
			_ => InCriticalCoinJoinState
		};
}
