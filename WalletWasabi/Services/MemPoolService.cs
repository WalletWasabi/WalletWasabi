using WalletWasabi.Models;
using NBitcoin;
using System;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Logging;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WalletWasabi.WebClients.Wasabi;

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

		private int _cleanupInProcess;

		/// <summary>
		/// Tries to perform mempool cleanup with the help of the backend.
		/// </summary>
		public async Task<bool> TryPerformMempoolCleanupAsync(Func<Uri> destAction, IPEndPoint torSocks)
		{
			// If already cleaning, then no need to run it that often.
			if (Interlocked.CompareExchange(ref _cleanupInProcess, 1, 0) == 1) return false;

			// This function is designed to prevent forever growing mempool.
			try
			{
				Logger.LogInfo<MemPoolService>("Start cleaning out mempool...");
				using (var client = new WasabiClient(destAction, torSocks))
				{
					var allMempoolHashes = await client.GetMempoolHashesAsync();

					uint256[] toRemove = TransactionHashes.Except(allMempoolHashes).ToArray();
					foreach (uint256 tx in toRemove)
					{
						TransactionHashes.TryRemove(tx);
					}
					Logger.LogInfo<MemPoolService>($"{toRemove.Count()} transactions were cleaned from mempool.");
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogWarning<MemPoolService>(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _cleanupInProcess, 0);
			}

			return false;
		}

		/// <summary>
		/// Tries to perform mempool cleanup with the help of the connected nodes.
		/// NOTE: This results in heavy network activity! https://github.com/zkSNACKs/WalletWasabi/issues/1273
		/// </summary>
		public async Task<bool> TryPerformMempoolCleanupAsync(NodesGroup nodes, CancellationToken cancel)
		{
			// If already cleaning, then no need to run it that often.
			if (Interlocked.CompareExchange(ref _cleanupInProcess, 1, 0) == 1) return false;

			// This function is designed to prevent forever growing mempool.
			try
			{
				var delay = TimeSpan.FromMinutes(1);
				if (nodes?.ConnectedNodes is null) return false;
				while (nodes.ConnectedNodes.Count != nodes.MaximumNodeConnection && nodes.ConnectedNodes.All(x => x.IsConnected))
				{
					if (cancel.IsCancellationRequested) return false;
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

						if (cancel.IsCancellationRequested) return false;
						uint256[] txs = node.GetMempool(cancel);
						if (cancel.IsCancellationRequested) return false;
						allTxs.UnionWith(txs);
						if (cancel.IsCancellationRequested) return false;
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
				Logger.LogInfo<MemPoolService>($"{toRemove.Count()} transactions were cleaned from mempool.");

				return true;
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

			return false;
		}
	}
}
