using System.Collections.Generic;
using System.Threading;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackingDataFactory
{
	public CoinJoinTrackingDataFactory(
		IWasabiHttpClientFactory httpClientFactory,
		RoundStateUpdater roundStatusUpdater,
		CancellationToken cancellationToken)
	{
		HttpClientFactory = httpClientFactory;
		RoundStatusUpdater = roundStatusUpdater;
		CancellationToken = cancellationToken;
	}

	public IWasabiHttpClientFactory HttpClientFactory { get; }
	public RoundStateUpdater RoundStatusUpdater { get; }
	public CancellationToken CancellationToken { get; }

	public CoinJoinTrackingData CreateCoinJoinTrackingData(Wallet wallet, IEnumerable<SmartCoin> coinCandidates)
	{
		var coinJoinClient = new CoinJoinClient(
			HttpClientFactory,
			new KeyChain(wallet.KeyManager),
			new InternalDestinationProvider(wallet.KeyManager),
			RoundStatusUpdater,
			wallet.ServiceConfiguration.MinAnonScoreTarget);

		return new CoinJoinTrackingData(wallet, coinJoinClient, coinCandidates, CancellationToken);
	}
}
