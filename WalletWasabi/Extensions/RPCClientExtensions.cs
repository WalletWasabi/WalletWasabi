using System;
using System.Threading.Tasks;
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
				catch
				{
					// Hopefully Bitcoin Core brainfart: https://github.com/bitcoin/bitcoin/issues/14431
					for (int i = 2; i <= 1008; i++)
					{
						try
						{
							return await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode);
						}
						catch
						{
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
						response = await rpc.TryEstimateSmartFeeAsync(confirmationTarget, estimateMode);
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
	}
}
