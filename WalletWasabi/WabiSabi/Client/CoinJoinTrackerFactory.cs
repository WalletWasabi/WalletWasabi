using System.Collections.Generic;
using System.Threading;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackerFactory
{
	public CoinJoinTrackerFactory(
		IWasabiHttpClientFactory httpClientFactory,
		RoundStateUpdater roundStatusUpdater,
		CancellationToken cancellationToken)
	{
		HttpClientFactory = httpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		CancellationToken = cancellationToken;
	}

	private IWasabiHttpClientFactory HttpClientFactory { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	private CancellationToken CancellationToken { get; }

	public CoinJoinTracker CreateAndStart(Wallet wallet, IEnumerable<SmartCoin> coinCandidates)
	{
		var coinJoinClient = new CoinJoinClient(
			HttpClientFactory,
			new KeyChain(wallet.KeyManager, wallet.Kitchen),
			new InternalDestinationProvider(wallet.KeyManager),
			RoundStatusUpdater,
			wallet.KeyManager.MinAnonScoreTarget,
			feeRateAverageTimeFrame: TimeSpan.FromHours(wallet.KeyManager.FeeRateMedianTimeFrameHours),
			doNotRegisterInLastMinuteTimeLimit: TimeSpan.FromMinutes(1));

		return new CoinJoinTracker(wallet, coinJoinClient, coinCandidates, CancellationToken);
	}
}
