using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackerFactory
{
	public CoinJoinTrackerFactory(
		Func<string, IWabiSabiApiRequestHandler> arenaRequestHandlerFactory,
		RoundStateProvider roundStatusProvider,
		CoinJoinConfiguration coinJoinConfiguration,
		InputVerifier inputVerifier,
		CancellationToken cancellationToken)
	{
		ArenaRequestHandlerFactory = arenaRequestHandlerFactory;
		_roundStatusProvider = roundStatusProvider;
		_coinJoinConfiguration = coinJoinConfiguration;
		_inputVerifier = inputVerifier;
		_cancellationToken = cancellationToken;
		_liquidityClueProvider = new LiquidityClueProvider();
	}

	private Func<string, IWabiSabiApiRequestHandler> ArenaRequestHandlerFactory { get; }
	private readonly RoundStateProvider _roundStatusProvider;
	private readonly CoinJoinConfiguration _coinJoinConfiguration;
	private readonly InputVerifier _inputVerifier;
	private readonly CancellationToken _cancellationToken;
	private readonly LiquidityClueProvider _liquidityClueProvider;

	public CoinJoinTracker CreateAndStart(Wallet wallet, Wallet outputWallet, Func<IEnumerable<SmartCoin>> coinCandidatesFunc, bool stopWhenAllMixed, bool overridePlebStop)
	{
		_liquidityClueProvider.InitLiquidityClue(wallet);

		if (wallet.KeyChain is null)
		{
			throw new NotSupportedException("Wallet has no key chain.");
		}

		// A device counts every output outside its coinjoin account as value leaving it and refuses to sign the
		// round, after the coordinator has its inputs, which bans them. So its outputs never go to another wallet.
		if (wallet.KeyManager.HasCoinJoinAccount)
		{
			outputWallet = wallet;
		}

		// The only use-case when we set consolidation mode to true, when we are mixing to another wallet.
		wallet.ConsolidationMode = outputWallet.WalletId != wallet.WalletId;

		var minAnonScoreForPayments = wallet.OnlyUsePrivateFundsForPayments ? wallet.AnonScoreTarget : 0;
		var coinJoinCoinSelector = CoinJoinCoinSelector.FromWallet(wallet);
		var coinSelector = CoinSelectors.ComposedCoinSelector([
			CoinSelectors.PrivatePaymentCoinSelector(wallet.BatchedPayments, minAnonScoreForPayments, coinJoinCoinSelector.SemiPrivateThreshold),
			coinJoinCoinSelector.SelectCoinsForRound]);
		var coinJoinClient = new CoinJoinClient(
			ArenaRequestHandlerFactory,
			wallet.KeyChain,
			outputWallet.OutputProvider,
			_roundStatusProvider,
			coinSelector,
			_coinJoinConfiguration.CappedBy(wallet.KeyChain),
			_inputVerifier,
			_liquidityClueProvider,
			doNotRegisterInLastMinuteTimeLimit: TimeSpan.FromMinutes(1),
			minAnonScoreForPayments: minAnonScoreForPayments);

		return new CoinJoinTracker(wallet, coinJoinClient, coinCandidatesFunc, stopWhenAllMixed, overridePlebStop, outputWallet, _cancellationToken);
	}
}
