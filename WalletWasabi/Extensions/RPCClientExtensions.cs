using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using FeeRateByConfirmationTarget = System.Collections.Generic.Dictionary<int, NBitcoin.FeeRate>;

namespace WalletWasabi.Extensions;

public static class RPCClientExtensions
{
	private const EstimateSmartFeeMode EstimateMode = EstimateSmartFeeMode.Conservative;

	public static async Task<EstimateSmartFeeResponse> EstimateConservativeSmartFeeAsync(this IRPCClient rpc, int confirmationTarget, CancellationToken cancellationToken = default)
	{
		var estimations = await rpc.EstimateAllFeeAsync(cancellationToken).ConfigureAwait(false);
		return new EstimateSmartFeeResponse
		{
			Blocks = confirmationTarget,
			FeeRate = estimations.GetFeeRate(confirmationTarget)
		};
	}

	private static EstimateSmartFeeResponse SimulateRegTestFeeEstimation(int confirmationTarget)
	{
		int satoshiPerByte = (Constants.SevenDaysConfirmationTarget + 1 + 6 - confirmationTarget) / 7;
		Money feePerK = Money.Satoshis(satoshiPerByte * 1000);
		FeeRate feeRate = new(feePerK);
		var resp = new EstimateSmartFeeResponse { Blocks = confirmationTarget, FeeRate = feeRate };
		return resp;
	}

	private static FeeRateByConfirmationTarget SimulateRegTestFeeEstimation() =>
		Constants.ConfirmationTargets
		.Select(target => SimulateRegTestFeeEstimation(target))
		.ToDictionary(x => x.Blocks, x => x.FeeRate);

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
	public static async Task<AllFeeEstimate> EstimateAllFeeAsync(this IRPCClient rpc, CancellationToken cancel = default)
	{
		var smartEstimations = rpc.Network == Network.RegTest
			? SimulateRegTestFeeEstimation()
			: await GetFeeEstimationsAsync(rpc, cancel).ConfigureAwait(false);

		var mempoolInfo = await rpc.GetMempoolInfoAsync(cancel).ConfigureAwait(false);
		var uptime = await rpc.UptimeAsync(cancel).ConfigureAwait(false);

		var finalEstimations = uptime < TimeSpan.FromHours(2)
			? smartEstimations
			: SmartEstimationsWithMempoolInfo(smartEstimations, mempoolInfo);

		return new AllFeeEstimate(finalEstimations);
	}

	private static FeeRateByConfirmationTarget SmartEstimationsWithMempoolInfo(FeeRateByConfirmationTarget smartEstimations, MemPoolInfo mempoolInfo)
	{
		var minEstimations = GetFeeEstimationsFromMempoolInfo(mempoolInfo);
		var minEstimationFor260Mb = minEstimations.GetValueOrDefault(260 / 4) ?? FeeRate.Zero;
		var minSanityFeeRate = FeeRate.Max(minEstimationFor260Mb, mempoolInfo.GetSanityFeeRate());
		var estimationForTarget2 = minEstimations.GetValueOrDefault(2) ?? FeeRate.Zero;
		var maxEstimationFor3Mb = estimationForTarget2 > FeeRate.Zero ? estimationForTarget2 : new FeeRate(5_000m);
		var maxSanityFeeRate = maxEstimationFor3Mb;

		var fixedEstimations = smartEstimations
			.GroupJoin(
				minEstimations,
				outer => outer.Key,
				inner => inner.Key,
				(outer, inner) => new { Estimation = outer, MinimumFromMemPool = inner })
			.SelectMany(
				x => x.MinimumFromMemPool.Any() ? x.MinimumFromMemPool : [KeyValuePair.Create(0, FeeRate.Zero)],
				(a, b) =>
				{
					var maxLowerBound = FeeRate.Max(a.Estimation.Value, b.Value);
					var maxMinFeeRate = FeeRate.Max(minSanityFeeRate, maxLowerBound);
					var minMaxFeeRate = FeeRate.Min(maxSanityFeeRate, maxMinFeeRate);
					return (
						Target: a.Estimation.Key,
						FeeRate: minMaxFeeRate);
				})
			.ToDictionary(x => x.Target, x => x.FeeRate);

		return fixedEstimations;
	}

	private static async Task<FeeRateByConfirmationTarget> GetFeeEstimationsAsync(IRPCClient rpc, CancellationToken cancel = default)
	{
		var batchClient = rpc.PrepareBatch();

		var rpcFeeEstimationTasks = Constants.ConfirmationTargets
			.Select(target => batchClient.EstimateSmartFeeAsync(target, EstimateMode))
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
			.Where(x => x.IsCompletedSuccessfully)
			.Select(x => (target: x.Result.Blocks, feeRate: x.Result.FeeRate))
			.DistinctBy(x => x.target)
			.ToDictionary(x => x.target, x => x.feeRate);
	}

	private static FeeRateByConfirmationTarget GetFeeEstimationsFromMempoolInfo(MemPoolInfo mempoolInfo)
	{
		if (mempoolInfo.Histogram is null || mempoolInfo.Histogram.Length == 0)
		{
			return new FeeRateByConfirmationTarget(0);
		}

		const int Kb = 1_000;
		const int Mb = 1_000 * Kb;
		const int BlockVirtualSize = 1 * Mb;
		static IEnumerable<(int Size, FeeRate From, FeeRate To)> SplitFeeGroupInBlocks(FeeRateGroup group)
		{
			var (q, rest) = Math.DivRem(group.Sizes, BlockVirtualSize);
			var gs = rest == 0
				? Enumerable.Repeat(1 * Mb, (int)q)
				: Enumerable.Repeat(1 * Mb, (int)q).Append((int)rest);
			return gs.Select(i => (i, group.From, group.To));
		}

		// Filter those groups with very high fee transactions (less than 0.1%).
		// This is because in case a few transactions pay unreasonably high fees
		// then we don't want our estimations to be affected by those rare cases.
		var histogram = mempoolInfo.Histogram.OrderByDescending(x => x.Group).ToArray();
		var relevantFeeGroups = histogram
			.Scan(0u, (acc, g) => acc + g.Count)
			.Zip(histogram, (x, y) => (AccumulativeCount: x, Group: y))
			.SkipWhile(x => x.AccumulativeCount < mempoolInfo.Size / 1_000)
			.Select(x => x.Group)
			.ToList();

		// Splits multi-megabyte fee rate groups in 1mb chunk
		// We need to count blocks (or 1MvB transaction chunks) so, in case fee
		// groups are bigger than 1MvB we split those in multiple 1MvB chunks.
		var splittedFeeGroups = relevantFeeGroups.SelectMany(SplitFeeGroupInBlocks);

		// Assigns the corresponding confirmation target to the set of fee groups.
		// We have multiple fee rate groups which size are in the range [0..1MvB)
		//
		// Example: imagine we have only 4 fee rate groups in the form (size, from, to)
		//      [(10kb, 400, 500) (55kb, 300, 400) (310kb, 200, 300) (700kb, 100, 200)]
		//
		// In this case the three first fee rate groups fit well in the next block so
		// they have target=1 while the fourth will need to wait and for that reason it
		// is target=2
		var accumulatedVirtualSizes = splittedFeeGroups
			.Select(x => x.Size)
			.Scan(0m, (acc, size) => acc + size);

		var feeGroupsByTarget = splittedFeeGroups.Zip(
			accumulatedVirtualSizes,
			(feeGroup, accumulatedVirtualSize) => (FeeRate: feeGroup.From, Target: (int)Math.Ceiling(1 + (accumulatedVirtualSize / BlockVirtualSize))));

		// Consolidates all the fee rate groups that share the same confirmation target.
		// Following the previous example we have the fee rate groups with target in the
		// form of (target, size, from, to)
		//      [(1, 10kb, 400) (1, 55kb, 300) (1, 310kb, 200) (2, 700kb, 100)]
		//
		// But what we need is the following:
		//      [(1, 200) (2, 100)]
		var consolidatedFeeGroupByTarget = feeGroupsByTarget
			.GroupBy(
				x => x.Target,
				(target, feeGroups) => (Target: target, FeeRate: feeGroups.Last().FeeRate));

		var feeRateByConfirmationTarget = consolidatedFeeGroupByTarget
			.ToDictionary(x => x.Target, x => x.FeeRate);

		return feeRateByConfirmationTarget;
	}


	public static async Task<bool> SupportsBlockFiltersAsync(this IRPCClient rpc, CancellationToken cancellationToken)
	{
		try
		{
			var blockHash = await rpc.GetBestBlockHashAsync(cancellationToken).ConfigureAwait(false);
			await rpc.GetBlockFilterAsync(blockHash, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (Exception e)
		{
			Logger.LogWarning("Bitcoin RPC interface failed to fetch block filters");
			Logger.LogWarning(e);
			return false;
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
}
