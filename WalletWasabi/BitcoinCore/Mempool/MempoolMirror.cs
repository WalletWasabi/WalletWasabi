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
using WalletWasabi.Logging;

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
		private object MempoolLock { get; } = new();

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var mempoolHashes = await Rpc.GetRawMempoolAsync().ConfigureAwait(false);

			uint256[] missing;
			lock (MempoolLock)
			{
				var toEvict = Mempool.Keys.Except(mempoolHashes);
				missing = mempoolHashes.Except(Mempool.Keys).ToArray();

				foreach (var txid in toEvict.ToHashSet())
				{
					Mempool.Remove(txid);
				}
			}

			HashSet<Transaction> toAdd = new();
			foreach (var chunk in missing.Distinct().ChunkBy(100))
			{
				foreach (var tx in Node.GetMempoolTransactions(chunk.ToArray(), cancel))
				{
					tx.PrecomputeHash(invalidateExisting: false, lazily: true);
					toAdd.Add(tx);
				}
			}

			lock (MempoolLock)
			{
				foreach (var tx in toAdd)
				{
					Mempool.Add(tx.GetHash(), tx);
				}
			}
		}
	}
}
