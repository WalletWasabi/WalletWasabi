using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
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
}
