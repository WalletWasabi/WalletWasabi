using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackingData : IDisposable
{
	private bool _disposedValue;

	public CoinJoinTrackingData(
		Wallet wallet,
		IWasabiHttpClientFactory httpClientFactory,
		RoundStateUpdater roundStatusUpdater,
		IEnumerable<SmartCoin> coinCandidates,
		CancellationToken cancellationToken)
	{
		Wallet = wallet;

		CoinJoinClient = new CoinJoinClient(
			httpClientFactory,
			new KeyChain(wallet.KeyManager),
			new InternalDestinationProvider(wallet.KeyManager),
			roundStatusUpdater,
			wallet.ServiceConfiguration.MinAnonScoreTarget);

		CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CoinJoinTask = CoinJoinClient.StartCoinJoinAsync(coinCandidates, CancellationTokenSource.Token);
		CoinCandidates = coinCandidates;
	}

	private CoinJoinClient CoinJoinClient { get; }
	private CancellationTokenSource CancellationTokenSource { get; }

	public Wallet Wallet { get; }
	public Task<bool> CoinJoinTask { get; }
	public IEnumerable<SmartCoin> CoinCandidates { get; }
	public bool IsCompleted => CoinJoinTask.IsCompleted;
	public bool InCriticalCoinJoinState => CoinJoinClient.InCriticalCoinJoinState;

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
