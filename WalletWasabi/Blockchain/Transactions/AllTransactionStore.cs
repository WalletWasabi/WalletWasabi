using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.Transactions;

public class AllTransactionStore : ITransactionStore, IAsyncDisposable
{
	public AllTransactionStore(string workFolderPath, Network network, bool migrateData = true)
	{
		WorkFolderPath = workFolderPath;

		string mempoolStoreDataSource;
		string confirmedStoreDataSource;

		if (workFolderPath == SqliteStorageHelper.InMemoryDatabase)
		{
			mempoolStoreDataSource = SqliteStorageHelper.InMemoryDatabase;
			confirmedStoreDataSource = SqliteStorageHelper.InMemoryDatabase;
		}
		else
		{
			IoHelpers.EnsureDirectoryExists(WorkFolderPath);
			mempoolStoreDataSource = Path.Combine(WorkFolderPath, "Mempool");
			confirmedStoreDataSource = Path.Combine(WorkFolderPath, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion);
		}

		MempoolStore = new TransactionStore(workFolderPath: mempoolStoreDataSource, network, migrateData);
		ConfirmedStore = new TransactionStore(workFolderPath: confirmedStoreDataSource, network, migrateData);
	}

	private string WorkFolderPath { get; }

	public TransactionStore MempoolStore { get; }
	public TransactionStore ConfirmedStore { get; }
	private object Lock { get; } = new();

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		using IDisposable _ = BenchmarkLogger.Measure();

		Stopwatch sw = Stopwatch.StartNew();

		var initTasks = new[]
		{
			MempoolStore.InitializeAsync($"{nameof(MempoolStore)}.{nameof(MempoolStore.InitializeAsync)}", cancellationToken),
			ConfirmedStore.InitializeAsync($"{nameof(ConfirmedStore)}.{nameof(ConfirmedStore.InitializeAsync)}", cancellationToken)
		};

		await Task.WhenAll(initTasks).ConfigureAwait(false);
		EnsureConsistency();

		long ms = sw.ElapsedMilliseconds;
		Logger.LogInfo($"XXX: Both stores initialized in {ms} ms");
	}

	public void AddOrUpdate(SmartTransaction tx)
	{
		lock (Lock)
		{
			var hash = tx.GetHash();

			if (tx.Confirmed)
			{
				if (MempoolStore.TryRemove(hash, out var found))
				{
					found.TryUpdate(tx);
					ConfirmedStore.TryAddOrUpdate(found);
				}
				else
				{
					ConfirmedStore.TryAddOrUpdate(tx);
				}
			}
			else
			{
				if (!ConfirmedStore.TryUpdate(tx))
				{
					MempoolStore.TryAddOrUpdate(tx);
				}
			}
		}
	}

	internal bool TryUpdate(SmartTransaction tx)
	{
		uint256 hash = tx.GetHash();

		lock (Lock)
		{
			// Do Contains first, because it's fast.
			if (ConfirmedStore.TryUpdate(tx))
			{
				return true;
			}
			else if (tx.Confirmed && MempoolStore.TryRemove(hash, out SmartTransaction? originalTx))
			{
				originalTx.TryUpdate(tx);
				ConfirmedStore.TryAddOrUpdate(originalTx);
				return true;
			}
			else if (MempoolStore.TryUpdate(tx))
			{
				return true;
			}
		}

		return false;
	}

	private void EnsureConsistency()
	{
		lock (Lock)
		{
			List<uint256> mempoolTransactions = MempoolStore.GetTransactionHashes();

			foreach (uint256 txid in mempoolTransactions)
			{
				// Contains is fast, so do this first.
				if (ConfirmedStore.Contains(txid)
					&& MempoolStore.TryRemove(txid, out var uTx))
				{
					ConfirmedStore.TryAddOrUpdate(uTx);
				}
			}
		}
	}

	public virtual bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? sameStx)
	{
		lock (Lock)
		{
			if (MempoolStore.TryGetTransaction(hash, out sameStx))
			{
				return true;
			}

			return ConfirmedStore.TryGetTransaction(hash, out sameStx);
		}
	}

	/// <returns>Transactions ordered by blockchain.</returns>
	public IEnumerable<SmartTransaction> GetTransactions()
	{
		lock (Lock)
		{
			return ConfirmedStore.GetTransactions().Concat(MempoolStore.GetTransactions()).OrderByBlockchain().ToList();
		}
	}

	internal IEnumerable<uint256> GetTransactionHashes()
	{
		lock (Lock)
		{
			return ConfirmedStore.GetTransactionHashes().Concat(MempoolStore.GetTransactionHashes()).ToList();
		}
	}

	internal bool IsEmpty()
	{
		lock (Lock)
		{
			return ConfirmedStore.IsEmpty() && MempoolStore.IsEmpty();
		}
	}

	/// <remarks>Only used by tests.</remarks>
	internal bool Contains(uint256 hash)
	{
		lock (Lock)
		{
			return ConfirmedStore.Contains(hash) || MempoolStore.Contains(hash);
		}
	}

	public IEnumerable<SmartTransaction> ReleaseToMempoolFromBlock(uint256 blockHash)
	{
		lock (Lock)
		{
			List<SmartTransaction> reorgedTxs = new();
			foreach (var txHash in ConfirmedStore
				.GetTransactions()
				.Where(tx => tx.BlockHash == blockHash)
				.Select(tx => tx.GetHash()))
			{
				if (ConfirmedStore.TryRemove(txHash, out var removedTx))
				{
					removedTx.SetUnconfirmed();

					if (MempoolStore.TryAdd(removedTx))
					{
						reorgedTxs.Add(removedTx);
					}
					else
					{
						_ = MempoolStore.TryUpdate(removedTx);
					}
				}
			}
			return reorgedTxs;
		}
	}

	/// <returns>Labels ordered by blockchain.</returns>
	public IEnumerable<LabelsArray> GetLabels() => GetTransactions().Select(x => x.Labels);

	public async ValueTask DisposeAsync()
	{
		await MempoolStore.DisposeAsync().ConfigureAwait(false);
		await ConfirmedStore.DisposeAsync().ConfigureAwait(false);
	}
}
