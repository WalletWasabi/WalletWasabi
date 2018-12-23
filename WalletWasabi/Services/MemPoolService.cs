using WalletWasabi.Models;
using NBitcoin;
using System;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Logging;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Services
{
	public class MemPoolService
	{
		public ConcurrentHashSet<uint256> TransactionHashes { get; }

		public event EventHandler<SmartTransaction> TransactionReceived;

		internal void OnTransactionReceived(SmartTransaction transaction) => TransactionReceived?.Invoke(this, transaction);

		public MemPoolService()
		{
			TransactionHashes = new ConcurrentHashSet<uint256>();
			_cleanupInProcess = 0;
		}

		private long _cleanupInProcess;

		public void TryPerformMempoolCleanupAsync(NodesGroup nodes, CancellationToken cancel)
		{
			if (Interlocked.Read(ref _cleanupInProcess) == 1) return; // If already cleaning, then no need to run it that often.
			Interlocked.Exchange(ref _cleanupInProcess, 1);
			Task.Run(async () =>
			{
				// This function is designed to prevent forever growing mempool.
				try
				{
					var delay = TimeSpan.FromMinutes(1);
					if (nodes?.ConnectedNodes is null) return;
					while (nodes.ConnectedNodes.Count != nodes.MaximumNodeConnection && nodes.ConnectedNodes.All(x => x.IsConnected))
					{
						if (cancel.IsCancellationRequested) return;
						Logger.LogInfo<MemPoolService>($"Not all nodes were in a connected state. Delaying mempool cleanup for {delay.TotalSeconds} seconds...");
						await Task.Delay(delay, cancel);
					}

					Logger.LogInfo<MemPoolService>("Start cleaning out mempool...");

					var allTxs = new HashSet<uint256>();
					foreach (Node node in nodes.ConnectedNodes)
					{
						try
						{
							if (!node.IsConnected) continue;

							if (cancel.IsCancellationRequested) return;
							uint256[] txs = node.GetMempool(cancel);
							if (cancel.IsCancellationRequested) return;
							allTxs.UnionWith(txs);
							if (cancel.IsCancellationRequested) return;
						}
						catch (Exception ex)
						{
							if ((ex is InvalidOperationException && ex.Message.StartsWith("The node is not in a connected state", StringComparison.InvariantCultureIgnoreCase))
								|| ex is OperationCanceledException
								|| ex is TaskCanceledException
								|| ex is TimeoutException)
							{
								Logger.LogTrace<MemPoolService>(ex);
							}
							else
							{
								Logger.LogDebug<MemPoolService>(ex);
							}
						}
					}

					uint256[] toRemove = TransactionHashes.Except(allTxs).ToArray();
					foreach (uint256 tx in toRemove)
					{
						TransactionHashes.TryRemove(tx);
					}
					Logger.LogInfo<MemPoolService>($"{toRemove.Count()} transactions were cleaned from mempool...");
				}
				catch (Exception ex)
				{
					if (ex is OperationCanceledException
						|| ex is TaskCanceledException
						|| ex is TimeoutException)
					{
						Logger.LogTrace<MemPoolService>(ex);
					}
					else
					{
						Logger.LogDebug<MemPoolService>(ex);
					}
				}
				finally
				{
					Interlocked.Exchange(ref _cleanupInProcess, 0);
				}
			});
		}
	}
}
