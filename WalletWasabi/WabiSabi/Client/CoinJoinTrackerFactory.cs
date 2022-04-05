using System.Collections.Generic;
using System.Threading;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackerFactory
{
	public CoinJoinTrackerFactory(
		IWasabiHttpClientFactory httpClientFactory,
		RoundStateUpdater roundStatusUpdater,
		string coordinatorIdentifier,
		CancellationToken cancellationToken)
	{
		HttpClientFactory = httpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		CoordinatorIdentifier = coordinatorIdentifier;
		CancellationToken = cancellationToken;
	}

	private IWasabiHttpClientFactory HttpClientFactory { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	private CancellationToken CancellationToken { get; }
	private string CoordinatorIdentifier { get; }

	public CoinJoinTracker CreateAndStart(Wallet wallet, IEnumerable<SmartCoin> coinCandidates, bool restartAutomatically, bool overridePlebStop)
	{
		var coinJoinClient = new CoinJoinClient(
			HttpClientFactory,
			new KeyChain(wallet.KeyManager, wallet.Kitchen),
			new InternalDestinationProvider(wallet.KeyManager),
			RoundStatusUpdater,
			CoordinatorIdentifier,
			wallet.KeyManager.AnonScoreTarget,
			feeRateMedianTimeFrame: TimeSpan.FromHours(wallet.KeyManager.FeeRateMedianTimeFrameHours),
			doNotRegisterInLastMinuteTimeLimit: TimeSpan.FromMinutes(1));

		return new CoinJoinTracker(wallet, coinJoinClient, coinCandidates, restartAutomatically, overridePlebStop, CancellationToken);
	}
}
