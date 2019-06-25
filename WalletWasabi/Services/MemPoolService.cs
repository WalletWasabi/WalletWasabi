using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class MemPoolService
	{
		// Transactions those we would reply to INV messages.
		private List<TransactionBroadcastEntry> BroadcastStore { get; }

		public BitcoinStore BitcoinStore { get; }
		public MempoolStore MempoolStore => BitcoinStore?.MempoolStore;
		public MempoolCache MempoolCache => MempoolStore?.MempoolCache;

		private object BroadcastStoreLock { get; }

		public event EventHandler<SmartTransaction> TransactionReceived;

		internal void OnTransactionReceived(SmartTransaction transaction) => TransactionReceived?.Invoke(this, transaction);

		public MemPoolService(BitcoinStore bitcoinStore)
		{
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
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
				if (MempoolCache.IsEmpty)
				{
					return true; // There's nothing to cleanup.
				}

				Logger.LogInfo<MemPoolService>("Start cleaning out mempool...");
				using (var client = new WasabiClient(destAction, torSocks))
				{
					var compactness = 10;
					ISet<string> allMempoolHashes = await client.GetMempoolHashesAsync(compactness);

					(int removedHashCount, int removedTxCount) = await BitcoinStore.MempoolStore.CleanupAsync(allMempoolHashes, compactness);

					Logger.LogInfo<MemPoolService>($"{removedHashCount} transaction hashes were cleaned from mempool.");
					Logger.LogInfo<MemPoolService>($"{removedTxCount} wallet relevant transactions were cleaned from mempool.");
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
	}
}
