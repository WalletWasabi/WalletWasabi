using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public partial class CoinJoinManager
{
	private async void StartCoinJoinAsync(
		StartCoinJoinCommand startCommand,
		ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins,
		CoinJoinTrackerFactory coinJoinTrackerFactory,
		ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts)
	{
		var walletToStart = startCommand.Wallet;

		if (trackedCoinJoins.TryGetValue(walletToStart.WalletId, out var tracker))
		{
			if (startCommand.StopWhenAllMixed != tracker.StopWhenAllMixed)
			{
				tracker.StopWhenAllMixed = startCommand.StopWhenAllMixed;
				walletToStart.LogDebug($"Cannot start coinjoin, because it is already running - but updated the value of {nameof(startCommand.StopWhenAllMixed)} to {startCommand.StopWhenAllMixed}.");
			}
			else
			{
				walletToStart.LogDebug("Cannot start coinjoin, because it is already running.");
			}

			// On cancelling the shutdown prevention, we need to set it back to false, otherwise we won't continue CJing.
			tracker.IsStopped = false;

			return;
		}

		var coinJoinTracker = await coinJoinTrackerFactory.CreateAndStartAsync(walletToStart, startCommand.OutputWallet, () => SanityChecksAndGetCoinCandidatesFuncAsync(walletToStart, startCommand), startCommand.StopWhenAllMixed, startCommand.OverridePlebStop).ConfigureAwait(false);

		if (!trackedCoinJoins.TryAdd(walletToStart.WalletId, coinJoinTracker))
		{
			// This should never happen.
			walletToStart.LogError($"{nameof(CoinJoinTracker)} was already added.");
			coinJoinTracker.Stop();
			coinJoinTracker.Dispose();
			return;
		}

		coinJoinTracker.WalletCoinJoinProgressChanged += CoinJoinTracker_WalletCoinJoinProgressChanged;

		var registrationTimeout = TimeSpan.MaxValue;
		NotifyCoinJoinStarted(walletToStart, registrationTimeout);

		walletToStart.LogDebug($"{nameof(CoinJoinClient)} started.");
		walletToStart.LogDebug($"{nameof(startCommand.StopWhenAllMixed)}:'{startCommand.StopWhenAllMixed}' {nameof(startCommand.OverridePlebStop)}:'{startCommand.OverridePlebStop}'.");

		// In case there was another start scheduled just remove it.
		TryRemoveTrackedAutoStart(trackedAutoStarts, walletToStart);
	}

	private async Task<IEnumerable<SmartCoin>> SanityChecksAndGetCoinCandidatesFuncAsync(IWallet walletToStart, StartCoinJoinCommand startCommand)
	{
		if (WalletsBlockedByUi.ContainsKey(walletToStart.WalletId))
		{
			throw new CoinJoinClientException(CoinjoinError.UserInSendWorkflow);
		}

		if (walletToStart.IsUnderPlebStop && !startCommand.OverridePlebStop)
		{
			walletToStart.LogTrace("PlebStop preventing coinjoin.");

			throw new CoinJoinClientException(CoinjoinError.NotEnoughUnprivateBalance);
		}

		if (WasabiBackendStatusProvide.LastResponse is not { } synchronizerResponse)
		{
			throw new CoinJoinClientException(CoinjoinError.BackendNotSynchronized);
		}

		var coinCandidates = await SelectCandidateCoinsAsync(walletToStart, synchronizerResponse.BestHeight).ConfigureAwait(false);

		// If there are pending payments, ignore already achieved privacy.
		if (!walletToStart.BatchedPayments.AreTherePendingPayments)
		{
			// If all coins are already private, then don't mix.
			if (await walletToStart.IsWalletPrivateAsync().ConfigureAwait(false))
			{
				walletToStart.LogTrace("All mixed!");
				throw new CoinJoinClientException(CoinjoinError.AllCoinsPrivate);
			}

			// If coin candidates are already private and the user doesn't override the StopWhenAllMixed, then don't mix.
			if (coinCandidates.All(x => x.IsPrivate(walletToStart.AnonScoreTarget)) && startCommand.StopWhenAllMixed)
			{
				throw new CoinJoinClientException(
					CoinjoinError.NoCoinsEligibleToMix,
					$"All coin candidates are already private and {nameof(startCommand.StopWhenAllMixed)} was {startCommand.StopWhenAllMixed}");
			}
		}

		NotifyWalletStartedCoinJoin(walletToStart);

		return coinCandidates;
	}

	private void StopCoinJoin(
		StopCoinJoinCommand stopCommand,
		ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins,
		ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts)
	{
		var walletToStop = stopCommand.Wallet;

		var autoStartRemoved = TryRemoveTrackedAutoStart(trackedAutoStarts, walletToStop);

		if (trackedCoinJoins.TryGetValue(walletToStop.WalletId, out var coinJoinTrackerToStop))
		{
			coinJoinTrackerToStop.Stop();
			if (coinJoinTrackerToStop.InCriticalCoinJoinState)
			{
				walletToStop.LogWarning("Coinjoin is in critical phase, it cannot be stopped - it won't restart later.");
			}
		}
		else if (autoStartRemoved)
		{
			NotifyWalletStoppedCoinJoin(walletToStop);
		}
	}

	#region UI Related methods

	public void WalletEnteredSendWorkflow(WalletId walletId)
	{
		if (CoinJoinClientStates.TryGetValue(walletId, out var stateHolder))
		{
			WalletsBlockedByUi.TryAdd(walletId, new UiBlockedStateHolder(NeedRestart: false, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet));
		}
	}

	public async Task WalletEnteredSendingAsync(Wallet wallet)
	{
		if (!WalletsBlockedByUi.ContainsKey(wallet.WalletId))
		{
			Logger.LogDebug("Wallet tried to enter sending but it was not in the send workflow.");
			return;
		}

		if (!CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug("Wallet tried to enter sending but state was missing.");
			return;
		}

		// Evaluate and set if we should restart after the send workflow.
		if (stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
		{
			WalletsBlockedByUi[wallet.WalletId] = new UiBlockedStateHolder(NeedRestart: true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet);
		}

		await StopAsync(wallet, CancellationToken.None).ConfigureAwait(false);
	}

	public void WalletLeftSendWorkflow(Wallet wallet)
	{
		if (!WalletsBlockedByUi.TryRemove(wallet.WalletId, out var stateHolder))
		{
			Logger.LogDebug("Wallet was not in send workflow but left it.");
			return;
		}

		if (stateHolder.NeedRestart)
		{
			Task.Run(async () => await StartAsync(wallet, stateHolder.OutputWallet, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, CancellationToken.None).ConfigureAwait(false));
		}
	}

	public async Task SignalToStopCoinjoinsAsync()
	{
		foreach (var wallet in await WalletProvider.GetWalletsAsync().ConfigureAwait(false))
		{
			if (CoinJoinClientStates.TryGetValue(wallet.WalletId, out var stateHolder) && stateHolder.CoinJoinClientState is not CoinJoinClientState.Idle)
			{
				if (!WalletsBlockedByUi.TryAdd(wallet.WalletId, new UiBlockedStateHolder(true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet)))
				{
					WalletsBlockedByUi[wallet.WalletId] = new UiBlockedStateHolder(true, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, stateHolder.OutputWallet);
				}
				await StopAsync((Wallet)wallet, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	public async Task RestartAbortedCoinjoinsAsync()
	{
		foreach (var wallet in await WalletProvider.GetWalletsAsync().ConfigureAwait(false))
		{
			if (WalletsBlockedByUi.TryRemove(wallet.WalletId, out var stateHolder) && stateHolder.NeedRestart)
			{
				await StartAsync(wallet, stateHolder.OutputWallet, stateHolder.StopWhenAllMixed, stateHolder.OverridePlebStop, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	#endregion
}
