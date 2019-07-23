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
	public class MempoolService
	{
		public ConcurrentHashSet<uint256> TransactionHashes { get; }

		// Transactions those we would reply to INV messages.
		private List<TransactionBroadcastEntry> BroadcastStore { get; }

		private object BroadcastStoreLock { get; }

		public event EventHandler<SmartTransaction> TransactionReceived;

		internal void OnTransactionReceived(SmartTransaction transaction) => TransactionReceived?.Invoke(this, transaction);

		public MempoolService()
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

				return found is null
					? false
					: true;
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
		public async Task<bool> TryPerformMempoolCleanupAsync(Func<Uri> destAction, EndPoint torSocks)
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

				Logger.LogInfo<MempoolService>("Start cleaning out mempool...");
				using (var client = new WasabiClient(destAction, torSocks))
				{
					var compactness = 10;
					var allMempoolHashes = await client.GetMempoolHashesAsync(compactness);

					var toRemove = TransactionHashes.Where(x => !allMempoolHashes.Contains(x.ToString().Substring(0, compactness)));

					int removedTxCount = 0;
					foreach (uint256 tx in toRemove)
					{
						if (TransactionHashes.TryRemove(tx))
						{
							removedTxCount++;
						}
					}

					Logger.LogInfo<MempoolService>($"{removedTxCount} transactions were cleaned from mempool.");
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogWarning<MempoolService>(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _cleanupInProcess, 0);
			}

			return false;
		}
	}
}
