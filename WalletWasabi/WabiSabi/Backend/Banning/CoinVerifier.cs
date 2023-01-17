using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifier
{
	// This should be much bigger than the possible input-reg period.
	private TimeSpan AbsoluteScheduleSanityTimeout { get; } = TimeSpan.FromDays(2);

	public CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, Whitelist whitelist, WabiSabiConfig wabiSabiConfig)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = whitelist;
		WabiSabiConfig = wabiSabiConfig;
	}

	private Whitelist Whitelist { get; }
	private WabiSabiConfig WabiSabiConfig { get; }
	private CoinJoinIdStore CoinJoinIdStore { get; }
	private CoinVerifierApiClient CoinVerifierApiClient { get; }
	private ConcurrentDictionary<Coin, (DateTimeOffset ScheduleTime, TaskCompletionSource<CoinVerifyResult> TaskCompletionSource)> CoinVerifyItems { get; } = new();

	public event EventHandler<Coin>? CoinBlackListed;

	public async Task<IEnumerable<CoinVerifyResult>> VerifyCoinsAsync(IEnumerable<Coin> coinsToCheck, CancellationToken cancellationToken)
	{
		using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(30));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);

		// Booting up the results with the default value - ban: no, remove: yes.
		Dictionary<Coin, CoinVerifyResult> coinVerifyItems = coinsToCheck.ToDictionary(coin => coin, coin => new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true));

		// Building up the task list.
		List<Task<CoinVerifyResult>> tasks = new();
		foreach (var coin in coinsToCheck)
		{
			if (!CoinVerifyItems.TryGetValue(coin, out var item))
			{
				// If the coin was not scheduled try to quickly schedule it - it should not happen.
				Logger.LogWarning($"Trying to re-schedule coin '{coin.Outpoint}' for verification.");

				// Quickly re-scheduling the missing items.
				ScheduleVerification(coin, cancellationToken, TimeSpan.Zero);
				if (!CoinVerifyItems.TryGetValue(coin, out item))
				{
					// This should not happen.
					Logger.LogError($"Coin '{coin.Outpoint}' cannot be re-scheduled for verification. The coin will be removed from the round.");
					continue;
				}
			}

			tasks.Add(item.TaskCompletionSource.Task);
		}

		try
		{
			do
			{
				var completedTask = await Task.WhenAny(tasks).WaitAsync(linkedCts.Token).ConfigureAwait(false);
				tasks.Remove(completedTask);
				var result = await completedTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);

				// The verification task fulfilled its purpose - clean up.
				CoinVerifyItems.TryRemove(result.Coin, out var _);

				// Update the default value with the real result.
				coinVerifyItems[result.Coin] = result;
			}
			while (tasks.Any());
		}
		catch (OperationCanceledException ex)
		{
			if (cancellationTokenSource.IsCancellationRequested)
			{
				Logger.LogError(ex);
			}

			// Otherwise just return.
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}

		CleanUp();

		return coinVerifyItems.Values.ToArray();
	}

	private void CleanUp()
	{
		// In a normal case the CoinVerifyItems removed right after queried in VerifyCoinsAsync. This is a sanity clean up.
		var now = DateTimeOffset.UtcNow;
		foreach (var (coin, item) in CoinVerifyItems)
		{
			if (now - item.ScheduleTime > AbsoluteScheduleSanityTimeout)
			{
				CoinVerifyItems.TryRemove(coin, out var _);

				// This should never happen.
				if (!item.TaskCompletionSource.Task.IsCompleted)
				{
					Logger.LogError($"Unfinished task was removed for coin: '{coin.Outpoint}'.");
				}
			}
		}
	}

	private bool CheckForFlags(ApiResponseItem response)
	{
		bool shouldBan = false;

		if (WabiSabiConfig.RiskFlags is null)
		{
			return shouldBan;
		}

		var flagIds = response.Cscore_section.Cscore_info.Select(cscores => cscores.Id);

		if (flagIds.Except(WabiSabiConfig.RiskFlags).Any())
		{
			var unknownIds = flagIds.Except(WabiSabiConfig.RiskFlags).ToList();
			unknownIds.ForEach(id => Logger.LogWarning($"Flag {id} is unknown for the backend!"));
		}

		shouldBan = flagIds.Any(id => WabiSabiConfig.RiskFlags.Contains(id));

		return shouldBan;
	}

	public void ScheduleVerification(Coin coin, DateTimeOffset inputRegistrationEndTime, CancellationToken cancellationToken, bool oneHop = false, int? confirmations = null)
	{
		var startTime = inputRegistrationEndTime - WabiSabiConfig.CoinVerifierStartBefore;
		var delayUntilStart = startTime - DateTimeOffset.UtcNow;
		ScheduleVerification(coin, cancellationToken, delayUntilStart, oneHop, confirmations);
	}

	public void ScheduleVerification(Coin coin, CancellationToken cancellationToken, TimeSpan? delayedStart = null, bool oneHop = false, int? confirmations = null)
	{
		TaskCompletionSource<CoinVerifyResult> taskCompletionSource = new();

		if (!CoinVerifyItems.TryAdd(coin, (DateTimeOffset.UtcNow, taskCompletionSource)))
		{
			Logger.LogWarning("Coin was already scheduled for verification.");
			return;
		}

		if (oneHop)
		{
			taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false));
			return;
		}

		if (Whitelist.TryGet(coin.Outpoint, out _))
		{
			taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false));
			return;
		}

		if (CoinJoinIdStore.Contains(coin.Outpoint.Hash))
		{
			taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false));
			return;
		}

		if (coin.Amount >= WabiSabiConfig.CoinVerifierRequiredConfirmationAmount)
		{
			if (confirmations is null || confirmations < WabiSabiConfig.CoinVerifierRequiredConfirmation)
			{
				taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true));
				return;
			}
		}

		_ = Task.Run(async () =>
		{
			using CancellationTokenSource absoluteTimeoutCts = new(AbsoluteScheduleSanityTimeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, absoluteTimeoutCts.Token);

			try
			{
				var delay = delayedStart.GetValueOrDefault(TimeSpan.Zero);

				// Sanity check.
				if (delay > AbsoluteScheduleSanityTimeout)
				{
					Logger.LogError($"Start delay '{delay}' was more than the abolute maximum '{AbsoluteScheduleSanityTimeout}' for coin '{coin.Outpoint}'.");
					delay = AbsoluteScheduleSanityTimeout;
				}

				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, linkedCts.Token).ConfigureAwait(false);
				}

				var apiResponseItem = await CoinVerifierApiClient.SendRequestAsync(coin.ScriptPubKey, linkedCts.Token).ConfigureAwait(false);
				var shouldBan = CheckForFlags(apiResponseItem);

				// We got a definetive answer.
				if (shouldBan)
				{
					CoinBlackListed?.Invoke(this, coin);
				}
				else
				{
					Whitelist.Add(coin.Outpoint);
				}

				taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: shouldBan, ShouldRemove: shouldBan));
			}
			catch (Exception ex)
			{
				taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true));
				Logger.LogError($"Coin verification was failed with '{ex}' for coin '{coin.Outpoint}'.");

				// Do not throw an exception here - unobserverved exception prevention.
			}
		}, cancellationToken);
	}
}
