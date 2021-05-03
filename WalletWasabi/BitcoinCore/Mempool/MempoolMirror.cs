using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;

namespace WalletWasabi.BitcoinCore.Mempool
{
	public class MempoolMirror : PeriodicRunner
	{
		/// <param name="period">How often to mirror the mempool.</param>
		public MempoolMirror(TimeSpan period, IRPCClient rpc, Node node) : base(period)
		{
			Rpc = rpc;
			Node = node;
		}

		public IRPCClient Rpc { get; }
		public Node Node { get; }
		private Dictionary<uint256, Transaction> Mempool { get; } = new();

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var mempoolHashes = await Rpc.GetRawMempoolAsync().ConfigureAwait(false);

			var missing = mempoolHashes.Except(Mempool.Keys);
			var toEvict = Mempool.Keys.Except(mempoolHashes);

			foreach (var txid in toEvict.ToHashSet())
			{
				Mempool.Remove(txid);
			}

			foreach (var chunk in missing.Distinct().ChunkBy(100))
			{
				foreach (var tx in Node.GetMempoolTransactions(chunk.ToArray(), cancel))
				{
					tx.PrecomputeHash(invalidateExisting: false, lazily: true);
					Mempool.Add(tx.GetHash(), tx);
				}
			}
		}
	}
}
