using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace NBitcoin.RPC
{
	public static class RPCClientExtensions
	{
		/// <summary>
		/// Waits for a specific new block and returns useful info about it.
		/// </summary>
		/// <param name="timeout">(int, optional, default=0) Time in milliseconds to wait for a response. 0 indicates no timeout.</param>
		/// <returns>Returns the current block on timeout or exit</returns>
		public static async Task<(Height height, uint256 hash)> WaitForNewBlockAsync(this RPCClient rpc, long timeout = 0)
		{
			var resp = await rpc.SendCommandAsync("waitfornewblock", timeout);
			return (int.Parse(resp.Result["height"].ToString()), uint256.Parse(resp.Result["hash"].ToString()));
		}

		/// <summary>
		/// Waits for a specific new block and returns useful info about it.
		/// </summary>
		/// <param name="blockHash">Block hash to wait for</param>
		/// <param name="timeout">(int, optional, default=0) Time in milliseconds to wait for a response. 0 indicates no timeout.</param>
		/// <returns>Returns the current block on timeout or exit</returns>
		public static async Task<(Height height, uint256 hash)> WaitForBlockAsync(this RPCClient rpc, uint256 blockHash, long timeout = 0)
		{
			var resp = await rpc.SendCommandAsync("waitforblock", blockHash, timeout);
			return (int.Parse(resp.Result["height"].ToString()), uint256.Parse(resp.Result["hash"].ToString()));
		}

		public static async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(this RPCClient rpc, int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, bool tryOtherFeeRates = true)
		{
			if (simulateIfRegTest && rpc.Network == Network.RegTest)
			{
				return SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
			}

			if (tryOtherFeeRates)
			{
				try
				{
					return await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode);
				}
				catch (RPCException ex)
				{
					Logger.LogTrace(ex);
					// Hopefully Bitcoin Core brainfart: https://github.com/bitcoin/bitcoin/issues/14431
					for (int i = 2; i <= Constants.SevenDaysConfirmationTarget; i++)
					{
						try
						{
							return await rpc.EstimateSmartFeeAsync(i, estimateMode);
						}
						catch (RPCException ex2)
						{
							Logger.LogTrace(ex2);
						}
					}
				}
				// Let's try one more time, whatever.
			}

			return await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode);
		}

		public static async Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(this RPCClient rpc, int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, bool tryOtherFeeRates = false)
		{
			if (simulateIfRegTest && rpc.Network == Network.RegTest)
			{
				return SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
			}

			if (tryOtherFeeRates)
			{
				EstimateSmartFeeResponse response = await rpc.TryEstimateSmartFeeAsync(confirmationTarget, estimateMode);
				if (response != null)
				{
					return response;
				}
				else
				{
					// Hopefully Bitcoin Core brainfart: https://github.com/bitcoin/bitcoin/issues/14431
					for (int i = 2; i <= Constants.SevenDaysConfirmationTarget; i++)
					{
						response = await rpc.TryEstimateSmartFeeAsync(i, estimateMode);
						if (response != null)
						{
							return response;
						}
					}
				}
				// Let's try one more time, whatever.
			}

			return await rpc.TryEstimateSmartFeeAsync(confirmationTarget, estimateMode);
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

		public static async Task<AllFeeEstimate> EstimateAllFeeAsync(this RPCClient rpc, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, bool tolerateBitcoinCoreBrainfuck = true)
		{
			var estimations = await rpc.EstimateHalfFeesAsync(new Dictionary<int, int>(), 2, 0, Constants.SevenDaysConfirmationTarget, 0, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
			var allFeeEstimate = new AllFeeEstimate(estimateMode, estimations);
			return allFeeEstimate;
		}

		private static async Task<Dictionary<int, int>> EstimateHalfFeesAsync(this RPCClient rpc, IDictionary<int, int> estimations, int smallTarget, int smallFee, int largeTarget, int largeFee, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, bool tolerateBitcoinCoreBrainfuck = true)
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

			if (smallFee == 0)
			{
				var smallFeeResult = await rpc.EstimateSmartFeeAsync(smallTarget, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
				smallFee = (int)Math.Ceiling(smallFeeResult.FeeRate.SatoshiPerByte);
				newEstimations.TryAdd(smallTarget, smallFee);
			}

			if (largeFee == 0)
			{
				var largeFeeResult = await rpc.EstimateSmartFeeAsync(largeTarget, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
				largeFee = (int)Math.Ceiling(largeFeeResult.FeeRate.SatoshiPerByte);
				largeTarget = largeFeeResult.Blocks;
				newEstimations.TryAdd(largeTarget, largeFee);
			}

			int halfTarget = (smallTarget + largeTarget) / 2;
			var halfFeeResult = await rpc.EstimateSmartFeeAsync(halfTarget, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
			int halfFee = (int)Math.Ceiling(halfFeeResult.FeeRate.SatoshiPerByte);
			halfTarget = halfFeeResult.Blocks;
			newEstimations.TryAdd(halfTarget, halfFee);

			if (smallFee != halfFee)
			{
				var smallEstimations = await rpc.EstimateHalfFeesAsync(newEstimations, smallTarget, smallFee, halfTarget, halfFee, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
				foreach (var est in smallEstimations)
				{
					newEstimations.TryAdd(est.Key, est.Value);
				}
			}
			if (largeFee != halfFee)
			{
				var largeEstimations = await rpc.EstimateHalfFeesAsync(newEstimations, halfTarget, halfFee, largeTarget, largeFee, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
				foreach (var est in largeEstimations)
				{
					newEstimations.TryAdd(est.Key, est.Value);
				}
			}

			return newEstimations;
		}

		/// <returns>(allowed, reject-reason)</returns>
		public static async Task<(bool accept, string rejectReason)> TestMempoolAcceptAsync(this RPCClient rpc, IEnumerable<Coin> coins)
		{
			// Check if mempool would accept a fake transaction created with the registered inputs.
			// This will catch ascendant/descendant count and size limits for example.
			var fakeTransaction = rpc.Network.CreateTransaction();
			fakeTransaction.Inputs.AddRange(coins.Select(coin => new TxIn(coin.Outpoint)));
			Money fakeOutputValue = NBitcoinHelpers.TakeAReasonableFee(coins.Sum(coin => coin.TxOut.Value));
			fakeTransaction.Outputs.Add(fakeOutputValue, new Key());
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
		public static async Task<IEnumerable<uint256>> GetUnconfirmedAsync(this RPCClient rpc, IEnumerable<uint256> transactionHashes)
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
		public static async Task<ISet<uint256>> GetAllDependentsAsync(this RPCClient rpc, IEnumerable<uint256> transactionHashes, bool includingProvided, bool likelyProvidedManyConfirmedOnes)
		{
			IEnumerable<uint256> workingTxHashes = likelyProvidedManyConfirmedOnes // If confirmed txIds are provided, then do a big check first.
				? await rpc.GetUnconfirmedAsync(transactionHashes)
				: transactionHashes;

			var hashSet = new HashSet<uint256>();
			foreach (var txId in workingTxHashes)
			{
				// Go through all the txIds provided and getmempoolentry to get the dependents and the confirmation status.
				var entry = await rpc.GetMempoolEntryAsync(txId, throwIfNotFound: false);
				if (entry != null)
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

		public static Block GetBlock(this RPCClient rpc, uint height)
			=> rpc.GetBlock((int)height);

		public static async Task<Block> GetBlockAsync(this RPCClient rpc, uint height)
			=> await rpc.GetBlockAsync((int)height);

		public static uint256 GetBlockHash(this RPCClient rpc, uint height)
			=> rpc.GetBlockHash((int)height);

		public static async Task<uint256> GetBlockHashAsync(this RPCClient rpc, uint height)
			=> await rpc.GetBlockHashAsync((int)height);

		public static BlockHeader GetBlockHeader(this RPCClient rpc, uint height)
			=> rpc.GetBlockHeader((int)height);

		public static async Task<BlockHeader> GetBlockHeaderAsync(this RPCClient rpc, uint height)
			=> await rpc.GetBlockHeaderAsync((int)height);
	}
}
