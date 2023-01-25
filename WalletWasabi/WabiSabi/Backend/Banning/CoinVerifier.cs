using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifier
{
	public CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, Whitelist whitelist, WabiSabiConfig wabiSabiConfig, string auditsDirectoryPath)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = whitelist;
		WabiSabiConfig = wabiSabiConfig;
		VerifierAuditArchiver = new CoinVerifierAuditArchiver(auditsDirectoryPath);
	}

	// Constructor used for testing
	internal CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, WabiSabiConfig wabiSabiConfig, CoinVerifierAuditArchiver? auditArchiver = null)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = new(Enumerable.Empty<Innocent>(), string.Empty, wabiSabiConfig);
		WabiSabiConfig = wabiSabiConfig;
		VerifierAuditArchiver = auditArchiver ?? new("test/directory/path");
	}

	public event EventHandler<Coin>? CoinBlacklisted;

	// This should be much bigger than the possible input-reg period.
	private TimeSpan AbsoluteScheduleSanityTimeout { get; } = TimeSpan.FromDays(2);

	private Whitelist Whitelist { get; }
	private WabiSabiConfig WabiSabiConfig { get; }
	private CoinJoinIdStore CoinJoinIdStore { get; }
	private CoinVerifierAuditArchiver VerifierAuditArchiver { get; }

	private CoinVerifierApiClient CoinVerifierApiClient { get; }
	private ConcurrentDictionary<Coin, CoinVerifyItem> CoinVerifyItems { get; } = new();

	public async Task<IEnumerable<CoinVerifyResult>> VerifyCoinsAsync(IEnumerable<Coin> coinsToCheck, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCancellationTokenSource = new(TimeSpan.FromSeconds(30));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

		// Booting up the results with the default value - ban: no, remove: yes.
		Dictionary<Coin, CoinVerifyResult> coinVerifyItems = coinsToCheck.ToDictionary(coin => coin, coin => new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true, Reason.NotChecked));

		// Building up the task list.
		List<Task<CoinVerifyResult>> tasks = new();
		foreach (var coin in coinsToCheck)
		{
			if (!CoinVerifyItems.TryGetValue(coin, out var item))
			{
				// If the coin was not scheduled try to quickly schedule it - it should not happen.
				Logger.LogWarning($"Trying to re-schedule coin '{coin.Outpoint}' for verification.");

				// Quickly re-scheduling the missing items - we do not want to cancel the verification after the local timeout, so passing cancellationToken.
				if (!TryScheduleVerification(coin, out item, cancellationToken, TimeSpan.Zero))
				{
					// This should not happen.
					Logger.LogError($"Coin '{coin.Outpoint}' cannot be re-scheduled for verification. The coin will be removed from the round.");
					continue;
				}
			}

			tasks.Add(item.Task);
		}

		try
		{
			while (tasks.Any())
			{
				var completedTask = await Task.WhenAny(tasks).WaitAsync(linkedCts.Token).ConfigureAwait(false);
				tasks.Remove(completedTask);
				var result = await completedTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);

				// The verification task fulfilled its purpose - clean up.
				if (CoinVerifyItems.TryRemove(result.Coin, out var item))
				{
					item.Dispose();
				}

				// Update the default value with the real result.
				coinVerifyItems[result.Coin] = result;
			}
		}
		catch (OperationCanceledException ex)
		{
			if (timeoutCancellationTokenSource.IsCancellationRequested)
			{
				Logger.LogError(ex);
			}

			// Otherwise just continue - the whole round was cancelled.
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}

		CleanUp();

		await Whitelist.WriteToFileIfChangedAsync().ConfigureAwait(false);

		await VerifierAuditArchiver.SaveAuditsAsync(coinVerifyItems.Values, cancellationToken).ConfigureAwait(false);

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
				if (!item.Task.IsCompleted)
				{
					Logger.LogError($"Unfinished task was removed for coin: '{coin.Outpoint}'.");
				}

				item.Dispose();
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

	public bool TryScheduleVerification(Coin coin, DateTimeOffset inputRegistrationEndTime, [NotNullWhen(true)] out CoinVerifyItem? coinVerifyItem, CancellationToken cancellationToken, bool oneHop = false, int? confirmations = null)
	{
		var startTime = inputRegistrationEndTime - WabiSabiConfig.CoinVerifierStartBefore;
		var delayUntilStart = startTime - DateTimeOffset.UtcNow;
		return TryScheduleVerification(coin, out coinVerifyItem, cancellationToken, delayUntilStart, oneHop, confirmations);
	}

	public bool TryScheduleVerification(Coin coin, [NotNullWhen(true)] out CoinVerifyItem? coinVerifyItem, CancellationToken verificationCancellationToken, TimeSpan? delayedStart = null, bool oneHop = false, int? confirmations = null)
	{
		var item = new CoinVerifyItem();
		coinVerifyItem = null;

		if (!CoinVerifyItems.TryAdd(coin, item))
		{
			Logger.LogWarning("Coin was already scheduled for verification.");
			item.Dispose();
			return false;
		}

		coinVerifyItem = item;

		if (oneHop)
		{
			item.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false, Reason.OneHop));
			return true;
		}

		if (Whitelist.TryGet(coin.Outpoint, out _))
		{
			item.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false, Reason.Whitelisted));
			return true;
		}

		if (CoinJoinIdStore.Contains(coin.Outpoint.Hash))
		{
			item.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false, Reason.Remix));
			return true;
		}

		if (coin.Amount >= WabiSabiConfig.CoinVerifierRequiredConfirmationAmount)
		{
			if (confirmations is null || confirmations < WabiSabiConfig.CoinVerifierRequiredConfirmations)
			{
				item.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true, Reason.Inmature));
				return true;
			}
		}

		_ = Task.Run(
			async () =>
			{
				using CancellationTokenSource absoluteTimeoutCts = new(AbsoluteScheduleSanityTimeout);
				using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(verificationCancellationToken, absoluteTimeoutCts.Token, item.Token);

				try
				{
					var delay = delayedStart.GetValueOrDefault(TimeSpan.Zero);

					// Sanity check.
					if (delay > AbsoluteScheduleSanityTimeout)
					{
						Logger.LogError($"Start delay '{delay}' was more than the absolute maximum '{AbsoluteScheduleSanityTimeout}' for coin '{coin.Outpoint}'.");
						delay = AbsoluteScheduleSanityTimeout;
					}

					if (delay > TimeSpan.Zero)
					{
						// We only abort and throw from the delay. If the API request already started, we will go with it.
						using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token, item.Token);
						await Task.Delay(delay, delayCts.Token).ConfigureAwait(false);
					}

					// This is the last chance to abort with abortCts.
					item.ThrowIfCancellationRequested();

					var apiResponseItem = await CoinVerifierApiClient.SendRequestAsync(coin.ScriptPubKey, linkedCts.Token).ConfigureAwait(false);
					var shouldBan = CheckForFlags(apiResponseItem);

					// We got a definitive answer.
					if (shouldBan)
					{
						CoinBlacklisted?.SafeInvoke(this, coin);
					}
					else
					{
						Whitelist.Add(coin.Outpoint);
					}

					item.SetResult(new CoinVerifyResult(coin, ShouldBan: shouldBan, ShouldRemove: shouldBan, Reason.RemoteApiChecked, apiResponseItem));
				}
				catch (Exception ex)
				{
					item.SetResult(new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true, Reason.Exception));
					Logger.LogError($"Coin verification was failed with '{ex}' for coin '{coin.Outpoint}'.");

					// Do not throw an exception here - unobserverved exception prevention.
				}
			},
			verificationCancellationToken);

		return true;
	}

	public void CancelSchedule(Coin coin)
	{
		if (CoinVerifyItems.TryGetValue(coin, out var item) && !item.IsCancellationRequested)
		{
			item.Cancel();
		}
	}
}
