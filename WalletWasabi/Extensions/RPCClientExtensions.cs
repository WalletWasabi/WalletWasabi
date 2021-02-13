using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace NBitcoin.RPC
{
	public static class RPCClientExtensions
	{
		public static async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(this IRPCClient rpc, int confirmationTarget, FeeRate sanityFeeRate, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			EstimateSmartFeeResponse result;
			if (simulateIfRegTest && rpc.Network == Network.RegTest)
			{
				result = SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
			}
			else
			{
				result = await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode).ConfigureAwait(false);
			}

			result.FeeRate = FeeRate.Max(sanityFeeRate, result.FeeRate);

			return result;
		}

		private static EstimateSmartFeeResponse SimulateRegTestFeeEstimation(int confirmationTarget, EstimateSmartFeeMode estimateMode)
		{
			int satoshiPerByte = estimateMode == EstimateSmartFeeMode.Conservative
				? (Constants.SevenDaysConfirmationTarget + 1 + 6 - confirmationTarget) / 7
				: (Constants.SevenDaysConfirmationTarget + 1 + 5 - confirmationTarget) / 7; // Economical

			Money feePerK = Money.Satoshis(satoshiPerByte * 1000);
			FeeRate feeRate = new FeeRate(feePerK);
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
		public static async Task<Exception?> TestAsync(this IRPCClient rpc)
		{
			try
			{
				await rpc.UptimeAsync().ConfigureAwait(false);
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
		public static async Task<AllFeeEstimate> EstimateAllFeeAsync(this IRPCClient rpc, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			var estimations = (simulateIfRegTest && rpc.Network == Network.RegTest)
				? SimulateRegTestFeeEstimation(estimateMode)
				: await GetFeeEstimationsAsync(rpc, estimateMode).ConfigureAwait(false);

			var rpcStatus = await rpc.GetRpcStatusAsync(CancellationToken.None).ConfigureAwait(false);
			var mempoolInfo = await rpc.GetMempoolInfoAsync().ConfigureAwait(false);
			var sanityFeeRate = mempoolInfo.GetSanityFeeRate();

			return new AllFeeEstimate(
				estimateMode,
				estimations.ToDictionary(x => x.Key, x => Math.Max(x.Value, (int)sanityFeeRate.SatoshiPerByte)),
				rpcStatus.Synchronized);
		}

		private static async Task<Dictionary<int, int>> GetFeeEstimationsAsync(IRPCClient rpc, EstimateSmartFeeMode estimateMode)
		{
			var batchClient = rpc.PrepareBatch();

			var rpcFeeEstimationTasks = Constants.ConfirmationTargets
				.Select(target => batchClient.EstimateSmartFeeAsync(target, estimateMode))
				.ToList();

			await batchClient.SendBatchAsync().ConfigureAwait(false);

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

			return rpcFeeEstimationTasks
				.Where(x => x.IsCompletedSuccessfully)
				.Select(x => x.Result)
				.ToDictionary(x => x.Blocks, x => (int)Math.Ceiling(x.FeeRate.SatoshiPerByte));
		}

		public static async Task<RpcStatus> GetRpcStatusAsync(this IRPCClient rpc, CancellationToken cancel)
		{
			try
			{
				var bci = await rpc.GetBlockchainInfoAsync().ConfigureAwait(false);
				cancel.ThrowIfCancellationRequested();
				var pi = await rpc.GetPeersInfoAsync().ConfigureAwait(false);

				return RpcStatus.Responsive(bci.Headers, bci.Blocks, pi.Length);
			}
			catch (Exception ex) when (ex is not OperationCanceledException and not TimeoutException)
			{
				Logger.LogTrace(ex);
				return RpcStatus.Unresponsive;
			}
		}

		public static async Task<(bool accept, string rejectReason)> TestMempoolAcceptAsync(this IRPCClient rpc, IEnumerable<Coin> coins, int fakeOutputCount, Money feePerInputs, Money feePerOutputs)
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
			MempoolAcceptResult testMempoolAcceptResult = await rpc.TestMempoolAcceptAsync(fakeTransaction, allowHighFees: true).ConfigureAwait(false);

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
		public static async Task<IEnumerable<uint256>> GetUnconfirmedAsync(this IRPCClient rpc, IEnumerable<uint256> transactionHashes)
		{
			uint256[] unconfirmedTransactionHashes = await rpc.GetRawMempoolAsync().ConfigureAwait(false);

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
		public static async Task<ISet<uint256>> GetAllDependentsAsync(this IRPCClient rpc, IEnumerable<uint256> transactionHashes, bool includingProvided, bool likelyProvidedManyConfirmedOnes)
		{
			IEnumerable<uint256> workingTxHashes = likelyProvidedManyConfirmedOnes // If confirmed txIds are provided, then do a big check first.
				? await rpc.GetUnconfirmedAsync(transactionHashes).ConfigureAwait(false)
				: transactionHashes;

			var hashSet = new HashSet<uint256>();
			foreach (var txId in workingTxHashes)
			{
				// Go through all the txIds provided and getmempoolentry to get the dependents and the confirmation status.
				var entry = await rpc.GetMempoolEntryAsync(txId, throwIfNotFound: false).ConfigureAwait(false);
				if (entry is { })
				{
					// If we asked to include the provided transaction hashes into the result then check which ones are confirmed and do so.
					if (includingProvided)
					{
						hashSet.Add(txId);
					}

					// Get all the dependents of all the dependents except the ones we already know of.
					var except = entry.Depends.Except(hashSet);
					var dependentsOfDependents = await rpc.GetAllDependentsAsync(except, includingProvided: true, likelyProvidedManyConfirmedOnes: false).ConfigureAwait(false);

					// Add them to the hashset.
					hashSet.UnionWith(dependentsOfDependents);
				}
			}

			return hashSet;
		}
	}
}
