using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifier : IAsyncDisposable
{
	public CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, Whitelist whitelist, WabiSabiConfig wabiSabiConfig, string auditsDirectoryPath)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = whitelist;
		WabiSabiConfig = wabiSabiConfig;
		VerifierAuditArchiver = new CoinVerifierLogger(auditsDirectoryPath);
	}

	// Constructor used for testing
	internal CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, WabiSabiConfig wabiSabiConfig, Whitelist? whitelist = null, CoinVerifierLogger? auditArchiver = null)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = whitelist ?? new(Enumerable.Empty<Innocent>(), string.Empty, wabiSabiConfig);
		WabiSabiConfig = wabiSabiConfig;
		VerifierAuditArchiver = auditArchiver ?? new("test/directory/path");
	}

	public event EventHandler<Coin>? CoinBlacklisted;

	// This should be much bigger than the possible input-reg period.
	private TimeSpan AbsoluteScheduleSanityTimeout { get; } = TimeSpan.FromDays(2);

	// Total API request timeout with retries. Do not forget to set this above CoinVerifierApiClient.ApiRequestTimeout.
	private TimeSpan TotalApiRequestTimeout { get; } = TimeSpan.FromMinutes(10);

	private Whitelist Whitelist { get; }
	private WabiSabiConfig WabiSabiConfig { get; }
	private CoinJoinIdStore CoinJoinIdStore { get; }
	public CoinVerifierLogger VerifierAuditArchiver { get; }

	private CoinVerifierApiClient CoinVerifierApiClient { get; }
	private ConcurrentDictionary<Coin, CoinVerifyItem> CoinVerifyItems { get; } = new(CoinEqualityComparer.Default);

	public async Task<IEnumerable<CoinVerifyResult>> VerifyCoinsAsync(IEnumerable<Coin> coinsToCheck, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCancellationTokenSource = new(TimeSpan.FromSeconds(30));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

		// Booting up the results with the default value - ban: no, remove: yes.
		Dictionary<Coin, CoinVerifyResult> coinVerifyResults = coinsToCheck.ToDictionary(
			coin => coin,
			coin => new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true),
			CoinEqualityComparer.Default);

		// Building up the task list.
		List<Task<CoinVerifyResult>> tasks = new();
		foreach (var coin in coinsToCheck)
		{
			// If the coin was not scheduled to be verified, then this method will return the default verification result for the coin - ban: no, remove: yes.
			if (CoinVerifyItems.TryGetValue(coin, out var item))
			{
				tasks.Add(item.Task);
			}
			else
			{
				Logger.LogWarning($"Coin {coin.Outpoint} is missing scheduled verification. Ignoring it.");
			}
		}

		try
		{
			while (tasks.Count != 0)
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
				coinVerifyResults[result.Coin] = result;
			}
		}
		catch (OperationCanceledException ex)
		{
			if (timeoutCancellationTokenSource.IsCancellationRequested)
			{
				Logger.LogWarning(ex);
			}

			// Otherwise just continue - the whole round was cancelled.
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}

		CleanUp();

		await Whitelist.WriteToFileIfChangedAsync().ConfigureAwait(false);

		await VerifierAuditArchiver.SaveAuditsAsync().ConfigureAwait(false);

		return coinVerifyResults.Values.ToArray();
	}

	private void CleanUp()
	{
		// In a normal case the CoinVerifyItems removed right after queried in VerifyCoinsAsync. This is a sanity clean up.
		var now = DateTimeOffset.UtcNow;
		foreach (var (coin, item) in CoinVerifyItems)
		{
			if (now - item.ScheduleTime > AbsoluteScheduleSanityTimeout)
			{
				CoinVerifyItems.TryRemove(coin, out _);

				// This should never happen.
				if (!item.Task.IsCompleted)
				{
					Logger.LogWarning($"Unfinished task was removed for coin: '{coin.Outpoint}'.");
				}

				item.Dispose();
			}
		}
	}

	private (bool ShouldBan, bool ShouldRemove) CheckVerifierResult(ApiResponseItem response, int blockchainHeightOfCoin)
	{
		if (WabiSabiConfig.RiskFlags is null)
		{
			return (false, false);
		}

		var flagIds = response.Cscore_section.Cscore_info.Select(cscores => cscores.Id);

		if (flagIds.Except(WabiSabiConfig.RiskFlags).Any())
		{
			var unknownIds = flagIds.Except(WabiSabiConfig.RiskFlags).ToList();
			unknownIds.ForEach(id => Logger.LogWarning($"Flag {id} is unknown for the backend!"));
		}

		bool shouldBan = flagIds.Any(id => WabiSabiConfig.RiskFlags.Contains(id));

		// When to remove:
		bool shouldRemove = shouldBan || // If we ban it.
			!response.Report_info_section.Address_used || // If address_used is false (API provider doesn't know about it).
			blockchainHeightOfCoin > response.Report_info_section.Report_block_height; // If the report_block_height is less than the block height of the coin. This means that the API provider didn't processed it, yet. On equal or if the report_height is bigger,then the API provider processed that block for sure.

		return (shouldBan, shouldRemove);
	}

	public bool TryScheduleVerification(Coin coin, DateTimeOffset inputRegistrationEndTime, int confirmations, bool oneHop, int currentBlockHeight, CancellationToken cancellationToken)
	{
		var startTime = inputRegistrationEndTime - WabiSabiConfig.CoinVerifierStartBefore;
		var delayUntilStart = startTime - DateTimeOffset.UtcNow;
		return TryScheduleVerification(coin, delayUntilStart, confirmations, oneHop, currentBlockHeight, cancellationToken);
	}

	public bool TryScheduleVerification(Coin coin, TimeSpan delayedStart, int confirmations, bool oneHop, int currentBlockHeight, CancellationToken verificationCancellationToken)
	{
		if (CoinVerifyItems.TryGetValue(coin, out _))
		{
			// Coin was already scheduled. It's OK.
			return true;
		}

		var item = new CoinVerifyItem();

		if (!CoinVerifyItems.TryAdd(coin, item))
		{
			Logger.LogWarning("Coin was already scheduled for verification.");
			item.Dispose();
			return false;
		}

		if (oneHop)
		{
			var result = new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false);
			item.SetResult(result);
			VerifierAuditArchiver.LogVerificationResult(result, Reason.OneHop);
			return true;
		}

		if (Whitelist.TryGet(coin.Outpoint, out _))
		{
			var result = new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false);
			item.SetResult(result);
			VerifierAuditArchiver.LogVerificationResult(result, Reason.Whitelisted);
			return true;
		}

		if (CoinJoinIdStore.Contains(coin.Outpoint.Hash))
		{
			var result = new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: false);
			item.SetResult(result);
			VerifierAuditArchiver.LogVerificationResult(result, Reason.Remix);
			return true;
		}

		if (coin.Amount >= WabiSabiConfig.CoinVerifierRequiredConfirmationAmount)
		{
			if (confirmations < WabiSabiConfig.CoinVerifierRequiredConfirmations)
			{
				var result = new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true);
				item.SetResult(result);
				VerifierAuditArchiver.LogVerificationResult(result, Reason.Immature);
				return true;
			}
		}

		Task.Run(
			async () =>
			{
				try
				{
					var delay = delayedStart;

					// Sanity check.
					if (delay > AbsoluteScheduleSanityTimeout)
					{
						Logger.LogWarning($"Start delay '{delay}' was more than the absolute maximum '{AbsoluteScheduleSanityTimeout}' for coin '{coin.Outpoint}'.");
						delay = AbsoluteScheduleSanityTimeout;
					}

					if (delay > TimeSpan.Zero)
					{
						// We only abort and throw from the delay. If the API request already started, we will go with it.
						using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(verificationCancellationToken, item.Token);
						await Task.Delay(delay, delayCts.Token).ConfigureAwait(false);
					}

					// This is the last chance to abort with abortCts.
					item.ThrowIfCancellationRequested();

					using CancellationTokenSource requestTimeoutCts = new(TotalApiRequestTimeout);
					using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(verificationCancellationToken, item.Token, requestTimeoutCts.Token);

					var apiResponseItem = await CoinVerifierApiClient.SendRequestAsync(coin.ScriptPubKey, linkedCts.Token).ConfigureAwait(false);

					// This calculates in which block the coin got into the blockchain.
					// So we can compare it to the latest block height that the API provider has already processed.
					int blockchainHeightOfCoin = currentBlockHeight - (confirmations - 1);

					(bool shouldBan, bool shouldRemove) = CheckVerifierResult(apiResponseItem, blockchainHeightOfCoin);

					// We got a definitive answer.
					if (shouldBan)
					{
						CoinBlacklisted?.SafeInvoke(this, coin);
					}
					else if (!shouldRemove)
					{
						Whitelist.Add(coin.Outpoint);
					}

					var result = new CoinVerifyResult(coin, ShouldBan: shouldBan, ShouldRemove: shouldRemove);
					item.SetResult(result);
					VerifierAuditArchiver.LogVerificationResult(result, Reason.RemoteApiChecked, apiResponseItem);
				}
				catch (Exception ex)
				{
					var result = new CoinVerifyResult(coin, ShouldBan: false, ShouldRemove: true);
					item.SetResult(result);
					VerifierAuditArchiver.LogVerificationResult(result, Reason.Exception, apiResponseItem: null, exception: ex);

					Logger.LogWarning($"Coin verification has failed for coin '{coin.Outpoint}' with '{ex}'.");

					// Do not throw an exception here - unobserved exception prevention.
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

	public ValueTask DisposeAsync()
	{
		return VerifierAuditArchiver.DisposeAsync();
	}
}
