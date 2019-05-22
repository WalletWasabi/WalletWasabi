using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class MemPoolService
	{
		public ConcurrentHashSet<uint256> TransactionHashes { get; }

		// Transactions those we would reply to INV messages.
		private List<TransactionBroadcastEntry> BroadcastStore { get; }

		private object BroadcastStoreLock { get; }

		public event EventHandler<SmartTransaction> TransactionReceived;

		internal void OnTransactionReceived(SmartTransaction transaction) => TransactionReceived?.Invoke(this, transaction);

		public MemPoolService()
		{
			TransactionHashes = new ConcurrentHashSet<uint256>();
			BroadcastStore = new List<TransactionBroadcastEntry>();
			BroadcastStoreLock = new object();
			_cleanupInProcess = 0;
		}

		public bool TryAddToBroadcastStore(Transaction transaction, string nodeRemoteSocketEndpoint)
		{
			lock (BroadcastStoreLock)
			{
				if (BroadcastStore.Any(x => x.TransactionId == transaction.GetHash()))
				{
					return false;
				}
				else
				{
					var entry = new TransactionBroadcastEntry(transaction, nodeRemoteSocketEndpoint);
					BroadcastStore.Add(entry);
					return true;
				}
			}
		}

		public bool TryRemoveFromBroadcastStore(uint256 transactionHash, out TransactionBroadcastEntry entry)
		{
			lock (BroadcastStoreLock)
			{
				var found = BroadcastStore.FirstOrDefault(x => x.TransactionId == transactionHash);
				entry = found;
				if (found is null)
				{
					return false;
				}
				else
				{
					BroadcastStore.RemoveAll(x => x.TransactionId == transactionHash);
					return true;
				}
			}
		}

		public bool TryGetFromBroadcastStore(uint256 transactionHash, out TransactionBroadcastEntry entry)
		{
			lock (BroadcastStoreLock)
			{
				var found = BroadcastStore.FirstOrDefault(x => x.TransactionId == transactionHash);
				entry = found;

				if (found is null)
				{
					return false;
				}
				else
				{
					return true;
				}
			}
		}

		public IEnumerable<TransactionBroadcastEntry> GetBroadcastStore()
		{
			lock (BroadcastStoreLock)
			{
				return BroadcastStore.ToList();
			}
		}

		private int _cleanupInProcess;

		/// <summary>
		/// Tries to perform mempool cleanup with the help of the backend.
		/// </summary>
		public async Task<bool> TryPerformMempoolCleanupAsync(Func<Uri> destAction, IPEndPoint torSocks)
		{
			// If already cleaning, then no need to run it that often.
			if (Interlocked.CompareExchange(ref _cleanupInProcess, 1, 0) == 1)
			{
				return false;
			}

			// This function is designed to prevent forever growing mempool.
			try
			{
				if (!TransactionHashes.Any())
				{
					return true; // There's nothing to cleanup.
				}

				Logger.LogInfo<MemPoolService>("Start cleaning out mempool...");
				using (var client = new WasabiClient(destAction, torSocks))
				{
					var compactness = 10;
					var allMempoolHashes = await client.GetMempoolHashesAsync(compactness).ConfigureAwait(false);

					var toRemove = TransactionHashes.Where(x => !allMempoolHashes.Any(y => y == x.ToString().Substring(0, compactness)));
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
			if (Interlocked.CompareExchange(ref _cleanupInProcess, 1, 0) == 1)
			{
				return false;
			}

			// This function is designed to prevent forever growing mempool.
			try
			{
				if (!TransactionHashes.Any())
				{
					return true; // There's nothing to cleanup.
				}

				var delay = TimeSpan.FromMinutes(1);
				if (nodes?.ConnectedNodes is null)
				{
					return false;
				}

				while (nodes.ConnectedNodes.Count != nodes.MaximumNodeConnection && nodes.ConnectedNodes.All(x => x.IsConnected))
				{
					if (cancel.IsCancellationRequested)
					{
						return false;
					}

					Logger.LogInfo<MemPoolService>($"Not all nodes were in a connected state. Delaying mempool cleanup for {delay.TotalSeconds} seconds...");
					await Task.Delay(delay, cancel).ConfigureAwait(false);
				}

				Logger.LogInfo<MemPoolService>("Start cleaning out mempool...");

				var allTxs = new HashSet<uint256>();
				foreach (Node node in nodes.ConnectedNodes)
				{
					try
					{
						if (!node.IsConnected)
						{
							continue;
						}

						if (cancel.IsCancellationRequested)
						{
							return false;
						}

						uint256[] txs = node.GetMempool(cancel);
						if (cancel.IsCancellationRequested)
						{
							return false;
						}

						allTxs.UnionWith(txs);
						if (cancel.IsCancellationRequested)
						{
							return false;
						}
					}
					catch (Exception ex) when ((ex is InvalidOperationException && ex.Message.StartsWith("The node is not in a connected state", StringComparison.InvariantCultureIgnoreCase))
											|| ex is OperationCanceledException
											|| ex is TaskCanceledException
											|| ex is TimeoutException)
					{
						Logger.LogTrace<MemPoolService>(ex);
					}
					catch (Exception ex)
					{
						Logger.LogDebug<MemPoolService>(ex);
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
			catch (Exception ex) when (ex is OperationCanceledException
									|| ex is TaskCanceledException
									|| ex is TimeoutException)
			{
				Logger.LogTrace<MemPoolService>(ex);
			}
			catch (Exception ex)
			{
				Logger.LogDebug<MemPoolService>(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _cleanupInProcess, 0);
			}

			return false;
		}
	}
}
