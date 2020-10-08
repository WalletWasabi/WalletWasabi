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
		public static async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(this IRPCClient rpc, int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			if (simulateIfRegTest && rpc.Network == Network.RegTest)
			{
				return SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
			}

			return await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode);
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

		/// <summary>
		/// If null is returned, no exception is thrown, so the test was successful.
		/// </summary>
		public static async Task<Exception> TestAsync(this IRPCClient rpc)
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

		public static async Task<AllFeeEstimate> EstimateAllFeeAsync(this IRPCClient rpc, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			var rpcStatus = await rpc.GetRpcStatusAsync(CancellationToken.None).ConfigureAwait(false);
			var estimations = await rpc.EstimateHalfFeesAsync(new Dictionary<int, int>(), 2, 0, Constants.SevenDaysConfirmationTarget, 0, estimateMode, simulateIfRegTest).ConfigureAwait(false);
			var allFeeEstimate = new AllFeeEstimate(estimateMode, estimations, rpcStatus.Synchronized);
			return allFeeEstimate;
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
			catch (Exception ex) when (!(ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException))
			{
				Logger.LogTrace(ex);
				return RpcStatus.Unresponsive;
			}
		}

		private static async Task<Dictionary<int, int>> EstimateHalfFeesAsync(this IRPCClient rpc, IDictionary<int, int> estimations, int smallTarget, int smallTargetFee, int largeTarget, int largeTargetFee, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			var newEstimations = new Dictionary<int, int>();
			foreach (var est in estimations)
			{
				newEstimations.TryAdd(est.Key, est.Value);
			}

			if (Math.Abs(smallTarget - largeTarget) <= 1)
			{
				return newEstimations;
			}

			if (smallTargetFee == 0)
			{
				var smallTargetFeeResult = await rpc.EstimateSmartFeeAsync(smallTarget, estimateMode, simulateIfRegTest);
				smallTargetFee = (int)Math.Ceiling(smallTargetFeeResult.FeeRate.SatoshiPerByte);
				newEstimations.TryAdd(smallTarget, smallTargetFee);
			}

			if (largeTargetFee == 0)
			{
				var largeTargetFeeResult = await rpc.EstimateSmartFeeAsync(largeTarget, estimateMode, simulateIfRegTest);
				largeTargetFee = (int)Math.Ceiling(largeTargetFeeResult.FeeRate.SatoshiPerByte);

				// Blocks should never be larger than the target that we asked for, so it's just a sanity check.
				largeTarget = Math.Min(largeTarget, largeTargetFeeResult.Blocks);
				newEstimations.TryAdd(largeTarget, largeTargetFee);
			}

			int halfTarget = (smallTarget + largeTarget) / 2;
			var halfFeeResult = await rpc.EstimateSmartFeeAsync(halfTarget, estimateMode, simulateIfRegTest);
			int halfTargetFee = (int)Math.Ceiling(halfFeeResult.FeeRate.SatoshiPerByte);

			// Blocks should never be larger than the target that we asked for, so it's just a sanity check.
			halfTarget = Math.Min(halfTarget, halfFeeResult.Blocks);
			newEstimations.TryAdd(halfTarget, halfTargetFee);

			if (smallTargetFee > halfTargetFee)
			{
				var smallEstimations = await rpc.EstimateHalfFeesAsync(newEstimations, smallTarget, smallTargetFee, halfTarget, halfTargetFee, estimateMode, simulateIfRegTest);
				foreach (var est in smallEstimations)
				{
					newEstimations.TryAdd(est.Key, est.Value);
				}
			}
			if (largeTargetFee < halfTargetFee)
			{
				var largeEstimations = await rpc.EstimateHalfFeesAsync(newEstimations, halfTarget, halfTargetFee, largeTarget, largeTargetFee, estimateMode, simulateIfRegTest);
				foreach (var est in largeEstimations)
				{
					newEstimations.TryAdd(est.Key, est.Value);
				}
			}

			return newEstimations;
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
			MempoolAcceptResult testMempoolAcceptResult = await rpc.TestMempoolAcceptAsync(fakeTransaction, allowHighFees: true);

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
			uint256[] unconfirmedTransactionHashes = await rpc.GetRawMempoolAsync();

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
				? await rpc.GetUnconfirmedAsync(transactionHashes)
				: transactionHashes;

			var hashSet = new HashSet<uint256>();
			foreach (var txId in workingTxHashes)
			{
				// Go through all the txIds provided and getmempoolentry to get the dependents and the confirmation status.
				var entry = await rpc.GetMempoolEntryAsync(txId, throwIfNotFound: false);
				if (entry is { })
				{
					// If we asked to include the provided transaction hashes into the result then check which ones are confirmed and do so.
					if (includingProvided)
					{
						hashSet.Add(txId);
					}

					// Get all the dependents of all the dependents except the ones we already know of.
					var except = entry.Depends.Except(hashSet);
					var dependentsOfDependents = await rpc.GetAllDependentsAsync(except, includingProvided: true, likelyProvidedManyConfirmedOnes: false);

					// Add them to the hashset.
					hashSet.UnionWith(dependentsOfDependents);
				}
			}

			return hashSet;
		}
	}
}
