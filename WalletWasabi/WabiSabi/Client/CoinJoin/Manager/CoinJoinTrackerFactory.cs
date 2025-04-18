using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackerFactory
{
	public CoinJoinTrackerFactory(
		Func<string, IWabiSabiApiRequestHandler> arenaRequestHandlerFactory,
		RoundStateUpdater roundStatusUpdater,
		CoinJoinConfiguration coinJoinConfiguration,
		CancellationToken cancellationToken)
	{
		ArenaRequestHandlerFactory = arenaRequestHandlerFactory;
		_roundStatusUpdater = roundStatusUpdater;
		_coinJoinConfiguration = coinJoinConfiguration;
		_cancellationToken = cancellationToken;
		_liquidityClueProvider = new LiquidityClueProvider();
	}

	private Func<string, IWabiSabiApiRequestHandler> ArenaRequestHandlerFactory { get; }
	private readonly RoundStateUpdater _roundStatusUpdater;
	private readonly CoinJoinConfiguration _coinJoinConfiguration;
	private readonly CancellationToken _cancellationToken;
	private readonly LiquidityClueProvider _liquidityClueProvider;

	public async Task<CoinJoinTracker> CreateAndStartAsync(IWallet wallet, IWallet? outputWallet, Func<Task<IEnumerable<SmartCoin>>> coinCandidatesFunc, bool stopWhenAllMixed, bool overridePlebStop)
	{
		await _liquidityClueProvider.InitLiquidityClueAsync(wallet).ConfigureAwait(false);

		if (wallet.KeyChain is null)
		{
			throw new NotSupportedException("Wallet has no key chain.");
		}

		// The only use-case when we set consolidation mode to true, when we are mixing to another wallet.
		wallet.ConsolidationMode = outputWallet is not null && outputWallet.WalletId != wallet.WalletId;

		var coinSelector = CoinJoinCoinSelector.FromWallet(wallet);
		var coinJoinClient = new CoinJoinClient(
			ArenaRequestHandlerFactory,
			wallet.KeyChain,
			outputWallet != null ? outputWallet.OutputProvider : wallet.OutputProvider,
			_roundStatusUpdater,
			coinSelector,
			_coinJoinConfiguration,
			_liquidityClueProvider,
			doNotRegisterInLastMinuteTimeLimit: TimeSpan.FromMinutes(1));

		return new CoinJoinTracker(wallet, coinJoinClient, coinCandidatesFunc, stopWhenAllMixed, overridePlebStop, outputWallet, _cancellationToken);
	}
}
