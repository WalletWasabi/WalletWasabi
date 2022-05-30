using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace NBitcoin.RPC;

public static class RPCClientExtensions
{
	public static async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(this IRPCClient rpc, int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, CancellationToken cancellationToken = default)
	{
		EstimateSmartFeeResponse result;
		if (simulateIfRegTest && rpc.Network == Network.RegTest)
		{
			result = SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
		}
		else
		{
			result = await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode, cancellationToken).ConfigureAwait(false);

			var mempoolInfo = await rpc.GetMempoolInfoAsync(cancellationToken).ConfigureAwait(false);
			result.FeeRate = FeeRate.Max(mempoolInfo.GetSanityFeeRate(), result.FeeRate);
		}

		return result;
	}

	private static EstimateSmartFeeResponse SimulateRegTestFeeEstimation(int confirmationTarget, EstimateSmartFeeMode estimateMode)
	{
		int satoshiPerByte = estimateMode == EstimateSmartFeeMode.Conservative
			? (Constants.SevenDaysConfirmationTarget + 1 + 6 - confirmationTarget) / 7
			: (Constants.SevenDaysConfirmationTarget + 1 + 5 - confirmationTarget) / 7; // Economical

		Money feePerK = Money.Satoshis(satoshiPerByte * 1000);
		FeeRate feeRate = new(feePerK);
		var resp = new EstimateSmartFeeResponse { Blocks = confirmationTarget, FeeRate = feeRate };
		return resp;
	}

	private static Dictionary<int, int> SimulateRegTestFeeEstimation(EstimateSmartFeeMode estimateMode) =>
		Constants.ConfirmationTargets
		.Select(target => SimulateRegTestFeeEstimation(target, estimateMode))
		.ToDictionary(x => x.Blocks, x => (int)Math.Ceiling(x.FeeRate.SatoshiPerByte));

	/// <summary>
	/// If null is returned, no exception is thrown, so the test was successful.
	/// </summary>
	public static async Task<Exception?> TestAsync(this IRPCClient rpc, CancellationToken cancellationToken = default)
	{
		try
		{
			await rpc.UptimeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			return ex;
		}

		return null;
	}

	/// <summary>
	/// Estimates fees for 1w, 3d, 1d, 12h, 6h, 3h, 1h, 30m, 20m.
	/// </summary>
	public static async Task<AllFeeEstimate> EstimateAllFeeAsync(this IRPCClient rpc, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, CancellationToken cancel = default)
	{
		var smartEstimations = (simulateIfRegTest && rpc.Network == Network.RegTest)
			? SimulateRegTestFeeEstimation(estimateMode)
			: await GetFeeEstimationsAsync(rpc, estimateMode, cancel).ConfigureAwait(false);

		var mempoolInfo = await rpc.GetMempoolInfoAsync(cancel).ConfigureAwait(false);

		var sanityFeeRate = mempoolInfo.GetSanityFeeRate();
		var rpcStatus = await rpc.GetRpcStatusAsync(cancel).ConfigureAwait(false);

		var minEstimations = GetFeeEstimationsFromMempoolInfo(mempoolInfo);

		var fixedEstimations = smartEstimations
			.GroupJoin(
				minEstimations,
				outer => outer.Key,
				inner => inner.Key,
				(outer, inner) => new { Estimation = outer, MinimumFromMemPool = inner })
			.SelectMany(x =>
				x.MinimumFromMemPool.DefaultIfEmpty(),
				(a, b) => (
					Target: a.Estimation.Key,
					FeeRate: Math.Max((int)sanityFeeRate.SatoshiPerByte, Math.Max(a.Estimation.Value, b.Value))))
			.ToDictionary(x => x.Target, x => x.FeeRate);

		return new AllFeeEstimate(
			estimateMode,
			fixedEstimations,
			rpcStatus.Synchronized);
	}

	private static async Task<Dictionary<int, int>> GetFeeEstimationsAsync(IRPCClient rpc, EstimateSmartFeeMode estimateMode, CancellationToken cancel = default)
	{
		var batchClient = rpc.PrepareBatch();

		var rpcFeeEstimationTasks = Constants.ConfirmationTargets
			.Select(target => batchClient.EstimateSmartFeeAsync(target, estimateMode))
			.ToList();

		await batchClient.SendBatchAsync(cancel).ConfigureAwait(false);
		cancel.ThrowIfCancellationRequested();

		try
		{
			await Task.WhenAll(rpcFeeEstimationTasks).ConfigureAwait(false);
		}
		catch
		{
			if (rpcFeeEstimationTasks.All(x => x.IsFaulted))
			{
				throw rpcFeeEstimationTasks[0].Exception?.InnerExceptions[0]
					?? new Exception($"{nameof(GetFeeEstimationsAsync)} failed to fetch fee estimations.");
			}
		}

		// EstimateSmartFeeAsync returns the block number where estimate was found - not always what the requested one.
		return rpcFeeEstimationTasks
			.Zip(Constants.ConfirmationTargets, (task, target) => (task, target))
			.Where(x => x.task.IsCompletedSuccessfully)
			.Select(x => (x.target, feeRate: x.task.Result.FeeRate))
			.ToDictionary(x => x.target, x => (int)Math.Ceiling(x.feeRate.SatoshiPerByte));
	}

	private static Dictionary<int, int> GetFeeEstimationsFromMempoolInfo(MemPoolInfo mempoolInfo)
	{
		if (mempoolInfo.Histogram is null || !mempoolInfo.Histogram.Any())
		{
			return new Dictionary<int, int>(0);
		}

		const int BlockSize = 1_000_000;
		static IEnumerable<(int Size, FeeRate From, FeeRate To)> SplitFeeGroupInBlocks(FeeRateGroup group)
		{
			var remainingSize = (long)group.Sizes;
			while (remainingSize > 0)
			{
				var chunckSize = (int)Math.Min(remainingSize, BlockSize);
				yield return (chunckSize, group.From, group.To);
				remainingSize -= BlockSize;
			}
		}
		// Filter those groups with very high fee transactions (less than 0.1%).
		// This is because in case a few transactions pay unreasonablely high fees
		// then we don't want our estimations to be affected by those rare cases.
		var relevantFeeGroups = mempoolInfo.Histogram
			.OrderByDescending(x => x.Group)
			.SkipWhile(x => x.Count < mempoolInfo.Size / 1_000);

		// Splits multi-megabyte fee rate groups in 1mb chunck
		// We need to count blocks (or 1MvB transaction chuncks) so, in case fee
		// groups are bigger than 1MvB we split those in multiple 1MvB chuncks.
		var splittedFeeGroups = relevantFeeGroups.SelectMany(x => SplitFeeGroupInBlocks(x));

		// Assigns the corresponding confirmation target to the set of fee groups.
		// We have multiple fee rate groups which size are in the range [0..1MvB)
		//
		// Example: imagine we have only 4 fee rate groups in the form (size, from, to)
		//      [(10kb, 400, 500) (55kb, 300, 400) (310kb, 200, 300) (700kb, 100, 200)]
		//
		// In this case the three first fee rate groups fit well in the next block so
		// they have target=1 while the fourth will need to wait and for that reason it
		// is target=2
		var accumulatedSizes = splittedFeeGroups
			.Select(x => x.Size)
			.Scan(0m, (acc, size) => acc + size);

		var feeGroupsByTarget = splittedFeeGroups.Zip(accumulatedSizes, (feeGroup, accumulatedSize) =>
			(FeeRate: feeGroup.From, Target: (int)Math.Ceiling(1 + accumulatedSize / BlockSize)));

		// Consolidates all the fee rate groups that share the same confirmation target.
		// Following the previous example we have the fee rate groups with target in the
		// form of (target, size, from, to)
		//      [(1, 10kb, 400) (1, 55kb, 300) (1, 310kb, 200) (2, 700kb, 100)]
		//
		// But what we need is the following:
		//      [(1, 200) (2, 100)]
		var consolidatedFeeGroupByTarget = feeGroupsByTarget
			.GroupBy(x => x.Target,
				(target, feeGroups) => (Target: target, FeeRate: feeGroups.LastOrDefault().FeeRate.SatoshiPerByte));

		return consolidatedFeeGroupByTarget.ToDictionary(x => x.Target, x => (int)Math.Ceiling(x.FeeRate));
	}

	public static async Task<RpcStatus> GetRpcStatusAsync(this IRPCClient rpc, CancellationToken cancel)
	{
		try
		{
			var bci = await rpc.GetBlockchainInfoAsync(cancel).ConfigureAwait(false);
			var pi = await rpc.GetPeersInfoAsync(cancel).ConfigureAwait(false);

			return RpcStatus.Responsive(bci.Headers, bci.Blocks, pi.Length);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
		{
			Logger.LogTrace(ex);
			return RpcStatus.Unresponsive;
		}
	}

	public static async Task<(bool accept, string rejectReason)> TestMempoolAcceptAsync(this IRPCClient rpc, IEnumerable<Coin> coins, int fakeOutputCount, Money feePerInputs, Money feePerOutputs, CancellationToken cancellationToken)
	{
		// Check if mempool would accept a fake transaction created with the registered inputs.
		// This will catch ascendant/descendant count and size limits for example.
		var fakeTransaction = rpc.Network.CreateTransaction();
		fakeTransaction.Inputs.AddRange(coins.Select(coin => new TxIn(coin.Outpoint)));
		Money totalFakeOutputsValue;
		try
		{
			totalFakeOutputsValue = NBitcoinHelpers.TakeFee(coins, fakeOutputCount, feePerInputs, feePerOutputs);
		}
		catch (InvalidOperationException ex)
		{
			return (false, ex.Message);
		}
		for (int i = 0; i < fakeOutputCount; i++)
		{
			var fakeOutputValue = totalFakeOutputsValue / fakeOutputCount;
			fakeTransaction.Outputs.Add(fakeOutputValue, new Key());
		}
		MempoolAcceptResult testMempoolAcceptResult = await rpc.TestMempoolAcceptAsync(fakeTransaction, cancellationToken).ConfigureAwait(false);

		if (!testMempoolAcceptResult.IsAllowed)
		{
			string rejected = testMempoolAcceptResult.RejectReason;

			if (!(rejected.Contains("mandatory-script-verify-flag-failed", StringComparison.OrdinalIgnoreCase)
				|| rejected.Contains("non-mandatory-script-verify-flag", StringComparison.OrdinalIgnoreCase)))
			{
				return (false, rejected);
			}
		}
		return (true, "");
	}

	/// <summary>
	/// Gets the transactions that are unconfirmed using getrawmempool.
	/// This is efficient when many transaction ids are provided.
	/// </summary>
	public static async Task<IEnumerable<uint256>> GetUnconfirmedAsync(this IRPCClient rpc, IEnumerable<uint256> transactionHashes, CancellationToken cancellationToken)
	{
		uint256[] unconfirmedTransactionHashes = await rpc.GetRawMempoolAsync(cancellationToken).ConfigureAwait(false);

		// If there are common elements, then there's unconfirmed.
		return transactionHashes.Intersect(unconfirmedTransactionHashes);
	}

	/// <summary>
	/// Recursively gathers all the dependents of the mempool transactions provided.
	/// </summary>
	/// <param name="transactionHashes">Mempool transactions to gather their dependents.</param>
	/// <param name="includingProvided">Should it include in the result the unconfirmed ones from the provided transactionHashes.</param>
	/// <param name="likelyProvidedManyConfirmedOnes">If many provided transactionHashes are not confirmed then it optimizes by doing a check in the beginning of which ones are unconfirmed.</param>
	/// <returns>All the dependents of the provided transactionHashes.</returns>
	public static async Task<ISet<uint256>> GetAllDependentsAsync(this IRPCClient rpc, IEnumerable<uint256> transactionHashes, bool includingProvided, bool likelyProvidedManyConfirmedOnes, CancellationToken cancellationToken)
	{
		IEnumerable<uint256> workingTxHashes = likelyProvidedManyConfirmedOnes // If confirmed txIds are provided, then do a big check first.
			? await rpc.GetUnconfirmedAsync(transactionHashes, cancellationToken).ConfigureAwait(false)
			: transactionHashes;

		var hashSet = new HashSet<uint256>();
		foreach (var txId in workingTxHashes)
		{
			// Go through all the txIds provided and getmempoolentry to get the dependents and the confirmation status.
			var entry = await rpc.GetMempoolEntryAsync(txId, throwIfNotFound: false, cancellationToken).ConfigureAwait(false);
			if (entry is { })
			{
				// If we asked to include the provided transaction hashes into the result then check which ones are confirmed and do so.
				if (includingProvided)
				{
					hashSet.Add(txId);
				}

				// Get all the dependents of all the dependents except the ones we already know of.
				var except = entry.Depends.Except(hashSet);
				var dependentsOfDependents = await rpc.GetAllDependentsAsync(except, includingProvided: true, likelyProvidedManyConfirmedOnes: false, cancellationToken).ConfigureAwait(false);

				// Add them to the hashset.
				hashSet.UnionWith(dependentsOfDependents);
			}
		}

		return hashSet;
	}
}
