using NBitcoin;
using NBitcoin.RPC;
using System.Threading.Tasks;

namespace WalletWasabi.Services
{
	/// <summary>
	/// UtxoProvider implements the mechanism for querying for utxo using the rpc interface.
	/// </summary>
	public class UtxoProvider : IUtxoProvider 
	{
		private RPCClient _rpc;

		public UtxoProvider(RPCClient rpc)
		{
			_rpc = rpc;
		}

		/// <summary>
		/// Get the requested utxo info from an rpc interface.
		/// </summary>
		/// <returns>The corresponding transaction output or null if it is not found</returns>
		public async Task<TxOut> GetUtxoAsync(uint256 txid, int index)
		{
			var getTxOutResponse = await _rpc.GetTxOutAsync(txid, index);
			return getTxOutResponse?.TxOut ?? null;
		}
	}
}
