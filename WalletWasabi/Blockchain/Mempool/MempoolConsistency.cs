using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.BlockstreamInfo;

namespace WalletWasabi.Blockchain.Mempool
{
	public class MempoolConsistency : PeriodicRunner
	{
		public MempoolConsistency(TimeSpan period, IRPCClient rpcClient, Network network) : base(period)
		{
			RpcClient = rpcClient;
			Network = network;

			// We only have blockstream info for now. I didn't find any other reliable block explorer.
			// blockchain.info -> doesn't work for mempool txs (returns empty string)
			// smartbit is a catastrophe in reliability
			// coinbase have no endpoints we need
			Syncers = new[] { new BlockstreamInfoClient(Network) };
		}

		public IMempoolSyncer[] Syncers { get; }
		public IRPCClient RpcClient { get; }
		public Network Network { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			using (BenchmarkLogger.Measure(LogLevel.Info, "Mempool Synchronization"))
			{
				var remoteMempoolIdsTasks = Syncers.Select(x => x.GetMempoolTransactionIdsAsync(cancel)).ToArray();
				var localMempoolIds = (await RpcClient.GetRawMempoolAsync().ConfigureAwait(false)).ToHashSet();
				cancel.ThrowIfCancellationRequested();

				var remoteMempoolIds = await Task.WhenAll(remoteMempoolIdsTasks).ConfigureAwait(false);
				cancel.ThrowIfCancellationRequested();

				// Let's try to acquire transactions those have consensus of all syncers to be in mempool
				// except in ours of course.
				HashSet<uint256> txsToAcquire = new();
				foreach (var txid in remoteMempoolIds.IntersectAll().Where(x => !localMempoolIds.Contains(x)))
				{
					txsToAcquire.Add(txid);
				}

				Logger.LogInfo($"{nameof(localMempoolIds)}: {localMempoolIds.Count}, {nameof(remoteMempoolIds)}{remoteMempoolIds.Length}, {nameof(txsToAcquire)}: {txsToAcquire.Count}.");

				// Don't send out too many requests at once.
				foreach (var txids in txsToAcquire.ChunkBy(7))
				{
					var txTasks = txids.Select(x => Syncers.RandomElement()?.GetTransactionAsync(x, cancel));

					foreach (var txTask in txTasks)
					{
						if (txTask is not null)
						{
							try
							{
								var tx = await txTask.ConfigureAwait(false);
								await RpcClient.SendRawTransactionAsync(tx).ConfigureAwait(false);
								cancel.ThrowIfCancellationRequested();
							}
							catch (Exception ex)
							{
								Logger.LogDebug(ex);
							}
						}
					}
				}
			}
		}

		public override void Dispose()
		{
			foreach (var syncer in Syncers)
			{
				syncer?.Dispose();
			}
			base.Dispose();
		}
	}
}
