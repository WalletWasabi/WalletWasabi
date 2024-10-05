using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Blockchain.Mempool;

public class MempoolService
{
	/// <summary>Denotes whether we are cleaning up the mempool at the moment or not.</summary>
	private int _cleanupInProcess = 0;

	private long _totalReceives = 0;
	private long _duplicatedReceives = 0;

	public event EventHandler<SmartTransaction>? TransactionReceived;

	/// <remarks>Guarded by <see cref="_processedLock"/>.</remarks>
	private readonly HashSet<uint256> _processedTransactionHashes = new();

	/// <summary>Guards <see cref="_processedTransactionHashes"/>.</summary>
	private readonly object _processedLock = new();

	/// <summary>Transactions that we would reply to INV messages.</summary>
	/// <remarks>Guarded by <see cref="_broadcastStoreLock"/>.</remarks>
	private readonly List<TransactionBroadcastEntry> _broadcastStore = new();

	/// <summary>Guards <see cref="_broadcastStore"/>.</summary>
	private readonly object _broadcastStoreLock = new();

	public bool TrustedNodeMode { get; set; }

	public bool TryAddToBroadcastStore(SmartTransaction transaction)
	{
		lock (_broadcastStoreLock)
		{
			if (_broadcastStore.Any(x => x.TransactionId == transaction.GetHash()))
			{
				return false;
			}

			var entry = new TransactionBroadcastEntry(transaction);
			_broadcastStore.Add(entry);
			return true;
		}
	}

	public bool TryGetFromBroadcastStore(uint256 transactionHash, [NotNullWhen(true)] out TransactionBroadcastEntry? entry)
	{
		lock (_broadcastStoreLock)
		{
			entry = _broadcastStore.FirstOrDefault(x => x.TransactionId == transactionHash);
			return entry is not null;
		}
	}

	public LabelsArray TryGetLabel(uint256 txid)
	{
		var label = LabelsArray.Empty;
		if (TryGetFromBroadcastStore(txid, out var entry))
		{
			label = entry.Transaction.Labels;
		}

		return label;
	}

	/// <summary>
	/// Tries to perform mempool cleanup with the help of the backend.
	/// </summary>
	public async Task<bool> TryPerformMempoolCleanupAsync(HttpClient httpClient)
	{
		// If already cleaning, then no need to run it that often.
		if (Interlocked.CompareExchange(ref _cleanupInProcess, 1, 0) == 1)
		{
			return false;
		}

		// This function is designed to prevent forever growing mempool.
		try
		{
			lock (_processedLock)
			{
				if (_processedTransactionHashes.Count == 0)
				{
					// There's nothing to cleanup.
					return true;
				}
			}

			Logger.LogInfo("Start cleaning out mempool...");
			{
				var compactness = 10;
				var wasabiClient = new WasabiClient(httpClient);
				var allMempoolHashes = await wasabiClient.GetMempoolHashesAsync(compactness).ConfigureAwait(false);

				int removedTxCount;

				lock (_processedLock)
				{
					removedTxCount = _processedTransactionHashes.RemoveWhere(x => !allMempoolHashes.Contains(x.ToString()[..compactness]));
				}

				Logger.LogInfo($"{removedTxCount} transactions were removed from mempool.");
			}

			// Display warning if total receives would be reached by duplicated receives.
			// Also reset the benchmarking.
			var totalReceived = Interlocked.Exchange(ref _totalReceives, 0);
			var duplicatedReceived = Interlocked.Exchange(ref _duplicatedReceives, 0);
			if (duplicatedReceived >= totalReceived && totalReceived != 0)
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
		lock (_processedLock)
		{
			return _processedTransactionHashes.Contains(txid);
		}
	}

	public void Process(Transaction tx)
	{
		SmartTransaction? txAdded = null;

		lock (_processedLock)
		{
			if (_processedTransactionHashes.Add(tx.GetHash()))
			{
				txAdded = new SmartTransaction(tx, Height.Mempool, labels: TryGetLabel(tx.GetHash()));
			}
			else
			{
				Interlocked.Increment(ref _duplicatedReceives);
			}
			Interlocked.Increment(ref _totalReceives);
		}

		if (txAdded is { })
		{
			TransactionReceived?.Invoke(this, txAdded);
		}
	}

	public bool TrySpend(SmartCoin coin, SmartTransaction tx)
	{
		var spent = false;
		lock (_broadcastStoreLock)
		{
			foreach (var foundCoin in _broadcastStore
				.SelectMany(x => x.Transaction.WalletInputs)
				.Concat(_broadcastStore.SelectMany(x => x.Transaction.WalletOutputs))
				.Where(x => x == coin))
			{
				foundCoin.SpenderTransaction = tx;
				spent = true;
			}
		}
		return spent;
	}
}
