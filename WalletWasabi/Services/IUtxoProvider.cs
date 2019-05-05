using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Services
{
	/// <summary>
	/// Abstracts the way the utxo are fetch.  
	/// </summary>
	public interface IUtxoProvider
	{
		Task<TxOut> GetUtxoAsync(uint256 txid, int index);
	}
}