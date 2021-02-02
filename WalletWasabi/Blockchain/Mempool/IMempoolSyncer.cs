using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.Mempool
{
	public interface IMempoolSyncer : IDisposable
	{
		public Task<IEnumerable<uint256>> GetMempoolTransactionIdsAsync(CancellationToken cancel);

		public Task<Transaction> GetTransactionAsync(uint256 txid, CancellationToken cancel);
	}
}
