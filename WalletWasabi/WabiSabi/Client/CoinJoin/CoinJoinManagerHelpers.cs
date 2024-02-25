using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using static WalletWasabi.WabiSabi.Client.CoinJoinManager;

namespace WalletWasabi.WabiSabi.Client.CoinJoin;

public static class CoinJoinManagerHelpers
{
	public static async Task WaitAndHandleResultOfTasksAsync(string logPrefix, params Task[] tasks)
	{
		foreach (var task in tasks)
		{
			try
			{
				await task.ConfigureAwait(false);
				Logger.LogInfo($"Task '{logPrefix}' finished successfully.");
			}
			catch (OperationCanceledException)
			{
				Logger.LogInfo($"Task '{logPrefix}' finished successfully by cancellation.");
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Task '{logPrefix}' finished but with an error: '{ex}'.");
			}
		}
	}

	public static ImmutableDictionary<WalletId, CoinJoinClientStateHolder> GetCoinJoinClientStates(IEnumerable<IWallet> wallets, ConcurrentDictionary<WalletId, CoinJoinTracker> trackedCoinJoins, ConcurrentDictionary<IWallet, TrackedAutoStart> trackedAutoStarts)
	{
		var coinJoinClientStates = ImmutableDictionary.CreateBuilder<WalletId, CoinJoinClientStateHolder>();
		foreach (var wallet in wallets)
		{
			CoinJoinClientStateHolder state = new(CoinJoinClientState.Idle, StopWhenAllMixed: true, OverridePlebStop: false, OutputWallet: wallet);

			if (trackedCoinJoins.TryGetValue(wallet.WalletId, out var coinJoinTracker) && !coinJoinTracker.IsCompleted)
			{
				var trackerState = coinJoinTracker.InCriticalCoinJoinState
					? CoinJoinClientState.InCriticalPhase
					: CoinJoinClientState.InProgress;

				state = new(trackerState, coinJoinTracker.StopWhenAllMixed, coinJoinTracker.OverridePlebStop, coinJoinTracker.OutputWallet);
			}
			else if (trackedAutoStarts.TryGetValue(wallet, out var autoStartTracker))
			{
				state = new(CoinJoinClientState.InSchedule, autoStartTracker.StopWhenAllMixed, autoStartTracker.OverridePlebStop, autoStartTracker.OutputWallet);
			}

			coinJoinClientStates.Add(wallet.WalletId, state);
		}

		return coinJoinClientStates.ToImmutable();
	}
}
