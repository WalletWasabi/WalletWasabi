using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		public MempoolMirror(TimeSpan period, IRPCClient rpc, P2pNode node) : base(period)
		{
			Rpc = rpc;
			Node = node;
		}

		public IRPCClient Rpc { get; }
		public P2pNode Node { get; }
		private Dictionary<uint256, Transaction> Mempool { get; } = new();
		private object MempoolLock { get; } = new();

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			var firstTime = !Mempool.Any();
			var sw = Stopwatch.StartNew();

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

			var toAdd = Node.GetMempoolTransactions(missing, cancel).ToHashSet();

			lock (MempoolLock)
			{
				foreach (var tx in toAdd)
				{
					Mempool.Add(tx.GetHash(), tx);
				}
			}

			sw.Stop();
			if (firstTime)
			{
				Logger.LogInfo($"{toAdd.Count} transactions were copied from the full node to the in-memory mempool within {sw.Elapsed.TotalSeconds} seconds.");
			}
		}
	}
}
