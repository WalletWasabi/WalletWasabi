using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Backend.Models;
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

			var finishedCoinJoinTracker = await PhaseWaitCoinJoinAsync(coins, stoppingToken).ConfigureAwait(false);

			await PhaseHandleCoinJoinFinalizationAsync(finishedCoinJoinTracker, stoppingToken).ConfigureAwait(false);
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

	private async Task<CoinJoinTracker> PhaseWaitCoinJoinAsync(IEnumerable<SmartCoin> coins, CancellationToken stoppingToken)
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

		await cjTask.ConfigureAwait(false);

		return coinJoinTracker;
	}

	private async Task PhaseHandleCoinJoinFinalizationAsync(CoinJoinTracker finishedCoinJoin, CancellationToken cancellationToken)
	{
		var wallet = finishedCoinJoin.Wallet;
		var batchedPayments = wallet.BatchedPayments;
		CoinJoinClientException? cjClientException = null;
		try
		{
			var result = await finishedCoinJoin.CoinJoinTask.ConfigureAwait(false);
			if (result is SuccessfulCoinJoinResult successfulCoinjoin)
			{
				CoinRefrigerator.Freeze(successfulCoinjoin.Coins);
				batchedPayments.MovePaymentsToFinished(successfulCoinjoin.UnsignedCoinJoin.GetHash());
				await MarkDestinationsUsedAsync(successfulCoinjoin.OutputScripts).ConfigureAwait(false);
				wallet.LogInfo($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was broadcast.");
			}
			else
			{
				wallet.LogInfo($"{nameof(CoinJoinClient)} finished. Coinjoin transaction was not broadcast.");
			}
		}
		catch (UnknownRoundEndingException ex)
		{
			// Assuming that the round might be broadcast but our client was not able to get the ending status.
			CoinRefrigerator.Freeze(ex.Coins);
			await MarkDestinationsUsedAsync(ex.OutputScripts).ConfigureAwait(false);
			Logger.LogDebug(ex);
		}
		catch (CoinJoinClientException clientException)
		{
			cjClientException = clientException;
			Logger.LogDebug(clientException);
		}
		catch (InvalidOperationException ioe)
		{
			Logger.LogWarning(ioe);
		}
		catch (OperationCanceledException)
		{
			if (finishedCoinJoin.IsStopped)
			{
				wallet.LogInfo($"{nameof(CoinJoinClient)} stopped.");
			}
			else
			{
				wallet.LogInfo($"{nameof(CoinJoinClient)} was cancelled.");
			}
		}
		catch (UnexpectedRoundPhaseException e)
		{
			// `UnexpectedRoundPhaseException` indicates an error in the protocol however,
			// temporarily we are shortening the circuit by aborting the rounds if
			// there are Alices that didn't confirm.
			// The fix is already done but the clients have to upgrade.
			wallet.LogInfo($"{nameof(CoinJoinClient)} failed with exception: '{e}'");
		}
		catch (WabiSabiProtocolException wpe) when (wpe.ErrorCode == WabiSabiProtocolErrorCode.WrongPhase)
		{
			// This can happen when the coordinator aborts the round in Signing phase because of detected double spend.
			wallet.LogInfo($"{nameof(CoinJoinClient)} failed with: '{wpe.Message}'");
		}
		catch (Exception e)
		{
			wallet.LogError($"{nameof(CoinJoinClient)} failed with exception: '{e}'");
		}
		finally
		{
			batchedPayments.MovePaymentsToPending();
		}

		// If any coins were marked for banning, store them to file
		if (finishedCoinJoin.BannedCoins.Count != 0)
		{
			foreach (var info in finishedCoinJoin.BannedCoins)
			{
				CoinPrison.Ban(info.Coin, info.BanUntilUtc);
			}
		}

		NotifyCoinJoinCompletion(finishedCoinJoin);

		// When to stop mixing:
		// - If stop was requested by user.
		// - If cancellation was requested.
		if (finishedCoinJoin.IsStopped
			|| cancellationToken.IsCancellationRequested)
		{
			NotifyWalletStoppedCoinJoin(wallet);
		}
		else if (await wallet.IsWalletPrivateAsync().ConfigureAwait(false))
		{
			NotifyCoinJoinStartError(wallet, CoinjoinError.AllCoinsPrivate);
			if (!finishedCoinJoin.StopWhenAllMixed)
			{
				// In auto CJ mode we never stop trying.
				ScheduleRestartAutomatically(wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
			}
			else
			{
				// We finished with CJ permanently.
				NotifyWalletStoppedCoinJoin(wallet);
			}
		}
		else if (cjClientException is not null)
		{
			// - If there was a CjClient exception, for example PlebStop or no coins to mix,
			// Keep trying, so CJ starts automatically when the wallet becomes mixable again.
			ScheduleRestartAutomatically(wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
			NotifyCoinJoinStartError(wallet, cjClientException.CoinjoinError);
		}
		else
		{
			wallet.LogInfo($"{nameof(CoinJoinClient)} restart automatically.");

			ScheduleRestartAutomatically(wallet, trackedAutoStarts, finishedCoinJoin.StopWhenAllMixed, finishedCoinJoin.OverridePlebStop, finishedCoinJoin.OutputWallet, cancellationToken);
		}

		if (!trackedCoinJoins.TryRemove(wallet.WalletId, out _))
		{
			wallet.LogWarning("Was not removed from tracked wallet list. Will retry in a few seconds.");
		}
		else
		{
			finishedCoinJoin.WalletCoinJoinProgressChanged -= CoinJoinTracker_WalletCoinJoinProgressChanged;
			finishedCoinJoin.Dispose();
		}
	}

	/// <summary>
	/// Mark all the outputs we had in any of our wallets used.
	/// </summary>
	private async Task MarkDestinationsUsedAsync(ImmutableList<Script> outputs)
	{
		var scripts = outputs.ToHashSet();
		var wallets = await WalletProvider.GetWalletsAsync().ConfigureAwait(false);
		foreach (var k in wallets)
		{
			var kc = k.KeyChain;
			var state = KeyState.Used;

			// Watch only wallets have no key chains.
			if (kc is null && k is Wallet w)
			{
				foreach (var hdPubKey in w.KeyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
				{
					w.KeyManager.SetKeyState(state, hdPubKey);
				}

				w.KeyManager.ToFile();
			}
			else
			{
				k.KeyChain?.TrySetScriptStates(state, scripts);
			}
		}
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
