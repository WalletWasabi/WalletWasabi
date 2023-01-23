using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifier
{
	public CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, Whitelist whitelist, WabiSabiConfig wabiSabiConfig)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = whitelist;
		WabiSabiConfig = wabiSabiConfig;
	}

	// Constructor used for testing
	internal CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, WabiSabiConfig wabiSabiConfig)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = new(Enumerable.Empty<Innocent>(), string.Empty, wabiSabiConfig);
		WabiSabiConfig = wabiSabiConfig;
	}

	public event EventHandler<Coin>? CoinBlacklisted;

	// This should be much bigger than the possible input-reg period.
	private TimeSpan AbsoluteScheduleSanityTimeout { get; } = TimeSpan.FromDays(2);

	public Whitelist Whitelist { get; }
	public WabiSabiConfig WabiSabiConfig { get; }
	private CoinJoinIdStore CoinJoinIdStore { get; }
	private CoinVerifierApiClient CoinVerifierApiClient { get; }
	private ConcurrentDictionary<Coin, (DateTimeOffset ScheduleTime, TaskCompletionSource<CoinVerifyResult> TaskCompletionSource, CancellationTokenSource AbortCts)> CoinVerifyItems { get; } = new();

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
			while (tasks.Any())
			{
				var completedTask = await Task.WhenAny(tasks).WaitAsync(linkedCts.Token).ConfigureAwait(false);
				tasks.Remove(completedTask);
				var result = await completedTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);

				// The verification task fulfilled its purpose - clean up.
				CoinVerifyItems.TryRemove(result.Coin, out var item);
				item.AbortCts.Dispose();

				// Update the default value with the real result.
				coinVerifyItems[result.Coin] = result;
			}
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

		await Whitelist.WriteToFileIfChangedAsync().ConfigureAwait(false);

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

				item.AbortCts.Dispose();
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
		var abortCts = new CancellationTokenSource();

		if (!CoinVerifyItems.TryAdd(coin, (DateTimeOffset.UtcNow, taskCompletionSource, abortCts)))
		{
			Logger.LogWarning("Coin was already scheduled for verification.");
			abortCts.Dispose();
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
			if (confirmations is null || confirmations < WabiSabiConfig.CoinVerifierRequiredConfirmations)
			{
				taskCompletionSource.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true));
				return;
			}
		}

		_ = Task.Run(
			async () =>
			{
				using CancellationTokenSource absoluteTimeoutCts = new(AbsoluteScheduleSanityTimeout);
				using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, absoluteTimeoutCts.Token, abortCts.Token);

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
						// We only abort and throw from the delay. If the API request already started, we will go with it.
						using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token, abortCts.Token);
						await Task.Delay(delay, delayCts.Token).ConfigureAwait(false);
					}

					// This is the last chance to abort with abortCts.
					abortCts.Token.ThrowIfCancellationRequested();

					var apiResponseItem = await CoinVerifierApiClient.SendRequestAsync(coin.ScriptPubKey, linkedCts.Token).ConfigureAwait(false);
					var shouldBan = CheckForFlags(apiResponseItem);

					// We got a definetive answer.
					if (shouldBan)
					{
						CoinBlacklisted?.SafeInvoke(this, coin);
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
			},
			cancellationToken);
	}

	public void CancelSchedule(Coin coin)
	{
		if (CoinVerifyItems.TryGetValue(coin, out var item) && !item.AbortCts.IsCancellationRequested)
		{
			item.AbortCts.Cancel();
		}
	}
}
