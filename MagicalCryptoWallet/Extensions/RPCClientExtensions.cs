using System.Threading.Tasks;

namespace NBitcoin.RPC
{
	public static class RPCClientExtensions
	{
		/// <summary>
		/// Waits for a specific new block and returns useful info about it.
		/// </summary>
		/// <param name="timeout">(int, optional, default=0) Time in milliseconds to wait for a response. 0 indicates no timeout.</param>
		/// <returns>Returns the current block on timeout or exit</returns>
		public static async Task<BlockInfo> WaitForNewBlockAsync(this RPCClient rpc, long timeout = 0)
		{
			var resp = await rpc.SendCommandAsync("waitfornewblock", timeout).ConfigureAwait(false);
			return new BlockInfo
			{
				Height = int.Parse(resp.Result["height"].ToString()),
				Hash = uint256.Parse(resp.Result["hash"].ToString())
			};
		}

		/// <summary>
		/// Waits for a specific new block and returns useful info about it.
		/// </summary>
		/// <param name="blockhash">Block hash to wait for</param>
		/// <param name="timeout">(int, optional, default=0) Time in milliseconds to wait for a response. 0 indicates no timeout.</param>
		/// <returns>Returns the current block on timeout or exit</returns>
		public static async Task<BlockInfo> WaitForBlockAsync(this RPCClient rpc, uint256 blockhash, long timeout = 0)
		{
			var resp = await rpc.SendCommandAsync("waitforblock", blockhash.ToString(), timeout).ConfigureAwait(false);
			return new BlockInfo
			{
				Height = int.Parse(resp.Result["height"].ToString()),
				Hash = uint256.Parse(resp.Result["hash"].ToString())
			};
		}
	}

	public class BlockInfo
	{
		public int Height { get; internal set; }
		public uint256 Hash { get; internal set; }
	} 
}