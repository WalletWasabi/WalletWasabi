using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.WalletCoinJoin;

public class WalletCoinJoinClient : BackgroundService
{
	public WalletCoinJoinClient(
		IWallet wallet,
		CoinJoinTrackerFactory coinJoinTrackerFactory,
		RoundStateUpdater roundStateUpdater,
		CoinRefrigerator coinRefrigerator,
		CoinPrison coinPrison,
		IWasabiBackendStatusProvider wasabiBackendStatusProvide)
	{
		Wallet = wallet;
		CoinJoinTrackerFactory = coinJoinTrackerFactory;
		RoundStateUpdater = roundStateUpdater;
		CoinRefrigerator = coinRefrigerator;
		CoinPrison = coinPrison;
		WasabiBackendStatusProvide = wasabiBackendStatusProvide;
	}

	public IWallet Wallet { get; }

	public IWallet OutputWallet { get; } // TODO: get outputwallet from somewhere.
	public CoinJoinTrackerFactory CoinJoinTrackerFactory { get; }
	public RoundStateUpdater RoundStateUpdater { get; }
	public CoinRefrigerator CoinRefrigerator { get; }
	public CoinPrison CoinPrison { get; }
	public IWasabiBackendStatusProvider WasabiBackendStatusProvide { get; }

	private RoundStateUpdater RoundStatusUpdater { get; }
	private CancellationToken CancellationToken { get; }
	private Channel<CoinJoinCommand> CommandChannel { get; } = Channel.CreateUnbounded<CoinJoinCommand>();

	public bool BlockedByUi { get; set; }

	public bool StopWhenAllMixed { get; set; }

	public bool OverridePlebStop { get; set; }

	public async Task StartAsync(IWallet wallet, IWallet outputWallet, bool stopWhenAllMixed, bool overridePlebStop, CancellationToken cancellationToken)
	{
		if (overridePlebStop && !wallet.IsUnderPlebStop)
		{
			// Turn off overriding if we went above the threshold meanwhile.
			overridePlebStop = false;
			wallet.LogDebug("Do not override PlebStop anymore we are above the threshold.");
		}

		await CommandChannel.Writer.WriteAsync(new StartCoinJoinCommand(wallet, outputWallet, stopWhenAllMixed, overridePlebStop), cancellationToken).ConfigureAwait(false);
	}

	public async Task StopAsync(Wallet wallet, CancellationToken cancellationToken)
	{
		await CommandChannel.Writer.WriteAsync(new StopCoinJoinCommand(wallet), cancellationToken).ConfigureAwait(false);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (Wallet.IsAutoCoinJoin)
		{
			await PhaseInitialDelayAsync(stoppingToken).ConfigureAwait(false);
		}
		else
		{
			await WaitForCommandAsync<StartCoinJoinCommand>(stoppingToken).ConfigureAwait(false);
		}

		do
		{
			if (!CoinJoinManagerHelper.CanWalletCoinJoin(Wallet, BlockedByUi))
			{
				continue;
			}

			// Get all the rounds.
			var roundStates = RoundStateUpdater.GetRoundStates();

			// Rounds where input registration is possible.
			var roundCandidates = CoinJoinManagerHelper.GetRoundsForCoinJoin(roundStates);

			// Rounds for this wallet.
			var roundsForWallet = CoinJoinManagerHelper.GetRoundsForWallet(Wallet, roundCandidates, RoundStateUpdater.CoinJoinFeeRateMedians);

			if (!roundsForWallet.Any())
			{
				continue;
			}

			// For now we just select the first one.
			var chosenRound = roundsForWallet.First();

			var coins = await SelectCandidateCoinsAsync(Wallet).ConfigureAwait(false);

			if (!(await CoinJoinManagerHelper.ShouldWalletStartCoinJoinAsync(Wallet, StopWhenAllMixed, OverridePlebStop, coins).ConfigureAwait(false)))
			{
				continue;
			}

			await PhaseWaitCoinJoinAsync(coins, stoppingToken).ConfigureAwait(false);
		}
		while (true);
	}

	private async Task PhaseInitialDelayAsync(CancellationToken stoppingToken)
	{
		// We wait 10 minutes before we start auto CoinJoin. The user might want to do something quick, we do not want to interfere.
		using CancellationTokenSource delayTimeout = new(TimeSpan.FromMinutes(10));
		using CancellationTokenSource combinedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, delayTimeout.Token);

		do
		{
			try
			{
				var command = await CommandChannel.Reader.ReadAsync(combinedCts.Token).ConfigureAwait(false);
				if (command is StartCoinJoinCommand)
				{
					return;
				}
			}
			catch (OperationCanceledException)
			{
				if (delayTimeout.IsCancellationRequested)
				{
					return;
				}
				throw;
			}
		}
		while (true);

		// We cannot get here.
	}

	private async Task<CoinJoinResult> PhaseWaitCoinJoinAsync(IEnumerable<SmartCoin> coins, CancellationToken stoppingToken)
	{
		Task<IEnumerable<SmartCoin>> GetCoins()
		{
			return Task.FromResult(coins);
		}

		using var coinJoinTracker = await CoinJoinTrackerFactory.CreateAndStartAsync(Wallet, OutputWallet, GetCoins, StopWhenAllMixed, OverridePlebStop).ConfigureAwait(false);

		using CancellationTokenSource commandTaskCts = new();
		using CancellationTokenSource combinedCts = CancellationTokenSource.CreateLinkedTokenSource(commandTaskCts.Token, stoppingToken);

		var cjTask = coinJoinTracker.CoinJoinTask;
		var commandTask = WaitForCommandAsync<StopCoinJoinCommand>(combinedCts.Token);

		await Task.WhenAny(cjTask, commandTask).ConfigureAwait(false);

		// Command was given, handle it.
		if (commandTask.IsCompletedSuccessfully)
		{
			await commandTask.ConfigureAwait(false);
			coinJoinTracker.Stop();
		}
		else
		{
			commandTaskCts.Cancel();
			try
			{
				await commandTask.ConfigureAwait(false);
			}
			catch
			{
			}
		}

		CoinJoinResult result = await cjTask.ConfigureAwait(false);

		return result;
	}

	private async Task WaitForCommandAsync<T>(CancellationToken stoppingToken) where T : CoinJoinCommand
	{
		do
		{
			var command = await CommandChannel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
			if (command is T)
			{
				return;
			}
		}
		while (true);

		// We cannot get here.
	}

	private async Task<IEnumerable<SmartCoin>> SelectCandidateCoinsAsync(IWallet walletToStart)
	{
		if (WasabiBackendStatusProvide.LastResponse is not { } synchronizerResponse)
		{
			throw new InvalidOperationException();
		}

		var bestHeight = synchronizerResponse.BestHeight;

		var coinCandidates = new CoinsView(await walletToStart.GetCoinjoinCoinCandidatesAsync().ConfigureAwait(false))
			.Available()
			.Where(x => !CoinRefrigerator.IsFrozen(x))
			.ToArray();

		// If there is no available coin candidates, then don't mix.
		if (coinCandidates.Length == 0)
		{
			throw new CoinJoinClientException(CoinjoinError.NoCoinsEligibleToMix, "No candidate coins available to mix.");
		}

		var bannedCoins = coinCandidates.Where(x => CoinPrison.TryGetOrRemoveBannedCoin(x, out _)).ToArray();
		var immatureCoins = coinCandidates.Where(x => x.Transaction.IsImmature(bestHeight)).ToArray();
		var unconfirmedCoins = coinCandidates.Where(x => !x.Confirmed).ToArray();
		var excludedCoins = coinCandidates.Where(x => x.IsExcludedFromCoinJoin).ToArray();

		coinCandidates = coinCandidates
			.Except(bannedCoins)
			.Except(immatureCoins)
			.Except(unconfirmedCoins)
			.Except(excludedCoins)
			.ToArray();

		if (coinCandidates.Length == 0)
		{
			var anyNonPrivateUnconfirmed = unconfirmedCoins.Any(x => !x.IsPrivate(walletToStart.AnonScoreTarget));
			var anyNonPrivateImmature = immatureCoins.Any(x => !x.IsPrivate(walletToStart.AnonScoreTarget));
			var anyNonPrivateBanned = bannedCoins.Any(x => !x.IsPrivate(walletToStart.AnonScoreTarget));
			var anyNonPrivateExcluded = excludedCoins.Any(x => !x.IsPrivate(walletToStart.AnonScoreTarget));

			var errorMessage = $"Coin candidates are empty! {nameof(anyNonPrivateUnconfirmed)}:{anyNonPrivateUnconfirmed} {nameof(anyNonPrivateImmature)}:{anyNonPrivateImmature} {nameof(anyNonPrivateBanned)}:{anyNonPrivateBanned} {nameof(anyNonPrivateExcluded)}:{anyNonPrivateExcluded}";

			if (anyNonPrivateUnconfirmed)
			{
				throw new CoinJoinClientException(CoinjoinError.NoConfirmedCoinsEligibleToMix, errorMessage);
			}

			if (anyNonPrivateImmature)
			{
				throw new CoinJoinClientException(CoinjoinError.OnlyImmatureCoinsAvailable, errorMessage);
			}

			if (anyNonPrivateBanned)
			{
				throw new CoinJoinClientException(CoinjoinError.CoinsRejected, errorMessage);
			}

			if (anyNonPrivateExcluded)
			{
				throw new CoinJoinClientException(CoinjoinError.OnlyExcludedCoinsAvailable, errorMessage);
			}
		}

		return coinCandidates;
	}

	public record CoinJoinCommand(IWallet Wallet);
	public record StartCoinJoinCommand(IWallet Wallet, IWallet OutputWallet, bool StopWhenAllMixed, bool OverridePlebStop) : CoinJoinCommand(Wallet);
	public record StopCoinJoinCommand(IWallet Wallet) : CoinJoinCommand(Wallet);
}
