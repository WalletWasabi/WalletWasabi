using System;
using System.Threading.Tasks;
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
			var resp = await rpc.SendCommandAsync("waitforblock", blockhash.ToString(), timeout);
			return (int.Parse(resp.Result["height"].ToString()), uint256.Parse(resp.Result["hash"].ToString()));
		}

		public static async Task<EstimateSmartFeeResponse> EstimateSmartFeeAsync(this RPCClient rpc, int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			if (simulateIfRegTest && rpc.Network == Network.RegTest)
			{
				return SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
			}

			return await rpc.EstimateSmartFeeAsync(confirmationTarget, estimateMode);
		}

		public static async Task<EstimateSmartFeeResponse> TryEstimateSmartFeeAsync(this RPCClient rpc, int confirmationTarget, EstimateSmartFeeMode estimateMode = EstimateSmartFeeMode.Conservative, bool simulateIfRegTest = false)
		{
			if (simulateIfRegTest && rpc.Network == Network.RegTest)
			{
				return SimulateRegTestFeeEstimation(confirmationTarget, estimateMode);
			}

			return await rpc.TryEstimateSmartFeeAsync(confirmationTarget, estimateMode);
		}


		public static async Task<RawTransactionInfo> GetRawTransactionInfoAsync(this RPCClient rpc, uint256 txId)
		{
			var request = new RPCRequest(RPCOperations.getrawtransaction, new object[]{ txId.ToString(), true });
			var response = await rpc.SendCommandAsync(request);
			var json = response.Result;
			return new RawTransactionInfo{
				Transaction = Transaction.Parse(json.Value<string>("hex")),
				TransactionId = uint256.Parse(json.Value<string>("txid")),
				TransactionTime = json["time"] != null ? NBitcoin.Utils.UnixTimeToDateTime(json.Value<long>("time")): (DateTimeOffset?)null,
				Hash = uint256.Parse(json.Value<string>("hash")),
				Size = json.Value<uint>("size"),
				VirtualSize = json.Value<uint>("vsize"),
				Version = json.Value<uint>("version"),
				LockTime = new LockTime(json.Value<uint>("locktime")),
				BlockHash = json["blockhash"] != null ? uint256.Parse(json.Value<string>("blockhash")): null,
				Confirmations = json.Value<uint>("confirmations"),
				BlockTime = json["blocktime"] != null ? NBitcoin.Utils.UnixTimeToDateTime(json.Value<long>("blocktime")) : (DateTimeOffset?)null
			};
		}

		private static EstimateSmartFeeResponse SimulateRegTestFeeEstimation(int confirmationTarget, EstimateSmartFeeMode estimateMode)
		{
			int staoshiPerBytes;
			if (estimateMode == EstimateSmartFeeMode.Conservative)
			{
				staoshiPerBytes = 6 + confirmationTarget;
			}
			else // Economical
			{
				staoshiPerBytes = 5 + confirmationTarget;
			}

			var resp = new EstimateSmartFeeResponse { Blocks = confirmationTarget, FeeRate = new FeeRate(new Money(staoshiPerBytes * 1000, MoneyUnit.Satoshi)) };
			return resp;
		}
	}

	public class BlockInfo
	{
		public int Height { get; internal set; }
		public uint256 Hash { get; internal set; }
	}

	public class RawTransactionInfo
	{
		public Transaction Transaction {get; internal set;}
		public uint256 TransactionId {get; internal set;}
		public uint256 Hash {get; internal set;}
		public uint Size {get; internal set;}
		public uint VirtualSize {get; internal set;}
		public uint Version {get; internal set;}
		public LockTime LockTime {get; internal set;}
		public uint256 BlockHash {get; internal set;}
		public uint Confirmations {get; internal set;}
		public DateTimeOffset? TransactionTime {get; internal set;}
		public DateTimeOffset? BlockTime {get; internal set;}
	} 
}
