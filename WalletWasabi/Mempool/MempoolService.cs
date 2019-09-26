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

namespace WalletWasabi.Mempool
{
	public class MempoolService
	{
		private HashSet<uint256> ProcessedTransactionHashes { get; }
		private object ProcessedLock { get; }

		// Transactions that we would reply to INV messages.
		private List<TransactionBroadcastEntry> BroadcastStore { get; }

		private object BroadcastStoreLock { get; }

		public event EventHandler<SmartTransaction> TransactionReceived;

		/// <summary>
		/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
		/// So it should not be assumed it's some singleton.
		/// </summary>
		public MempoolBehavior CreateMempoolBehavior() => new MempoolBehavior(this);

		public MempoolService()
		{
			ProcessedTransactionHashes = new HashSet<uint256>();
			ProcessedLock = new object();
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
				// No need for locking when accessing Count.
				if (ProcessedTransactionHashes.Count == 0)
				{
					// There's nothing to cleanup.
					return true;
				}

				Logger.LogInfo("Start cleaning out mempool...");
				using (var client = new WasabiClient(destAction, torSocks))
				{
					var compactness = 10;
					var allMempoolHashes = await client.GetMempoolHashesAsync(compactness);

					lock (ProcessedLock)
					{
						int removedTxCount = ProcessedTransactionHashes.RemoveWhere(x => !allMempoolHashes.Contains(x.ToString().Substring(0, compactness)));

						Logger.LogInfo($"{removedTxCount} transactions were cleaned from mempool.");
					}
				}

				// Display warning if total receives would be reached by duplicated receives.
				// Also reset the benchmarking.
				var totalReceived = Interlocked.Exchange(ref _totalReceives, 0);
				var duplicatedReceived = Interlocked.Exchange(ref _duplicatedReceives, 0);
				if (duplicatedReceived >= totalReceived)
				{
					// Note that the worst case scenario is not duplicatedReceived == totalReceived, but duplicatedReceived == (number of peers) * totalReceived.
					// It's just duplicatedReceived == totalReceived is maximum what we want to tolerate.
					// By turning off Tor, we can notice that the ratio is much better, so this mainly depends on the internet speed.
					Logger.LogWarning($"Too many duplicated mempool transactions are downloaded.\n{nameof(duplicatedReceived)} : {duplicatedReceived}\n{nameof(totalReceived)} : {totalReceived}");
				}

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _cleanupInProcess, 0);
			}

			return false;
		}

		public bool IsProcessed(uint256 txid)
		{
			lock (ProcessedLock)
			{
				return ProcessedTransactionHashes.Contains(txid);
			}
		}

		private long _totalReceives = 0;
		private long _duplicatedReceives = 0;

		public void Process(Transaction tx)
		{
			lock (ProcessedLock)
			{
				if (ProcessedTransactionHashes.Add(tx.GetHash()))
				{
					TransactionReceived?.Invoke(this, new SmartTransaction(tx, Height.Mempool));
				}
				else
				{
					Interlocked.Increment(ref _duplicatedReceives);
				}

				Interlocked.Increment(ref _totalReceives);
			}
		}
	}
}
