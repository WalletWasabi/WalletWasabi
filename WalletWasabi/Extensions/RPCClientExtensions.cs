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
		/// <param name="blockhash">Block hash to wait for</param>
		/// <param name="timeout">(int, optional, default=0) Time in milliseconds to wait for a response. 0 indicates no timeout.</param>
		/// <returns>Returns the current block on timeout or exit</returns>
		public static async Task<(Height height, uint256 hash)> WaitForBlockAsync(this RPCClient rpc, uint256 blockhash, long timeout = 0)
		{
			var resp = await rpc.SendCommandAsync("waitforblock", blockhash, timeout);
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
					Logger.LogTrace<RPCClient>(ex);
					// Hopefully Bitcoin Core brainfart: https://github.com/bitcoin/bitcoin/issues/14431
					for (int i = 2; i <= 1008; i++)
					{
						try
						{
							return await rpc.EstimateSmartFeeAsync(i, estimateMode);
						}
						catch (RPCException ex2)
						{
							Logger.LogTrace<RPCClient>(ex2);
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
					for (int i = 2; i <= 1008; i++)
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
			int staoshiPerBytes;
			if (estimateMode == EstimateSmartFeeMode.Conservative)
			{
				staoshiPerBytes = ((1008 + 1 + 6) - confirmationTarget) / 7;
			}
			else // Economical
			{
				staoshiPerBytes = ((1008 + 1 + 5) - confirmationTarget) / 7;
			}

			Money feePerK = new Money(staoshiPerBytes * 1000, MoneyUnit.Satoshi);
			FeeRate feeRate = new FeeRate(feePerK);
			var resp = new EstimateSmartFeeResponse { Blocks = confirmationTarget, FeeRate = feeRate };
			return resp;
		}

		public static async Task<AllFeeEstimate> EstimateAllFeeAsync(this RPCClient rpc, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false, bool tolerateBitcoinCoreBrainfuck = true)
		{
			var estimations = await rpc.EstimateHalfFeesAsync(new Dictionary<int, int>(), 2, 0, 1008, 0, estimateMode, simulateIfRegTest, tolerateBitcoinCoreBrainfuck);
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
		/// Decides if any of the transactions are unconfirmed using getrawmempool.
		/// This is efficient when many transaction ids are provided.
		/// </summary>
		public static async Task<bool> AnyUnconfirmedAsync(this RPCClient rpcClient, ISet<uint256> transactionHashes)
		{
			uint256[] unconfirmedTransactionHashes = await rpcClient.GetRawMempoolAsync();
			if (transactionHashes.Intersect(unconfirmedTransactionHashes).Any()) // If there are common elements, then there's unconfirmed.
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}
