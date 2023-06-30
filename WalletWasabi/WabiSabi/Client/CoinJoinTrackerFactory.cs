using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
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
		LiquidityClueProvider = new LiquidityClueProvider();
	}

	private IWasabiHttpClientFactory HttpClientFactory { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	private CancellationToken CancellationToken { get; }
	private string CoordinatorIdentifier { get; }
	private LiquidityClueProvider LiquidityClueProvider { get; }

	public async Task<CoinJoinTracker> CreateAndStartAsync(IWallet wallet, Func<Task<IEnumerable<SmartCoin>>> coinCandidatesFunc, bool stopWhenAllMixed, bool overridePlebStop)
	{
		await LiquidityClueProvider.InitLiquidityClueAsync(wallet).ConfigureAwait(false);

		if (wallet.KeyChain is null)
		{
			throw new NotSupportedException("Wallet has no key chain.");
		}

		var coinSelector = CoinJoinCoinSelector.FromWallet(wallet);
		var outputProvider = new OutputProvider(wallet.DestinationProvider, InsecureRandom.Instance);
		var coinJoinClient = new CoinJoinClient(
			HttpClientFactory,
			wallet.KeyChain,
			outputProvider,
			RoundStatusUpdater,
			CoordinatorIdentifier,
			coinSelector,
			LiquidityClueProvider,
			feeRateMedianTimeFrame: wallet.FeeRateMedianTimeFrame,
			skipFactors: wallet.CoinjoinSkipFactors,
			doNotRegisterInLastMinuteTimeLimit: TimeSpan.FromMinutes(1));

		return new CoinJoinTracker(wallet, coinJoinClient, coinCandidatesFunc, stopWhenAllMixed, overridePlebStop, CancellationToken);
	}
}
