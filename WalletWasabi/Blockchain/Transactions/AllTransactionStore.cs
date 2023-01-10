using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Blockchain.Transactions;

public class AllTransactionStore : ITransactionStore, IAsyncDisposable
{
	public AllTransactionStore(string workFolderPath, Network network)
	{
		WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
		IoHelpers.EnsureDirectoryExists(WorkFolderPath);

		Network = network;

		MempoolStore = new TransactionStore(workFolderPath: Path.Combine(WorkFolderPath, "Mempool"), network);
		ConfirmedStore = new TransactionStore(workFolderPath: Path.Combine(WorkFolderPath, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion), network);
	}

	#region Initializers

	private string WorkFolderPath { get; }
	private Network Network { get; }

	public TransactionStore MempoolStore { get; }
	public TransactionStore ConfirmedStore { get; }
	private object Lock { get; } = new object();

	public async Task InitializeAsync(CancellationToken cancel = default)
	{
		using (BenchmarkLogger.Measure())
		{
			var initTasks = new[]
			{
				MempoolStore.InitializeAsync($"{nameof(MempoolStore)}.{nameof(MempoolStore.InitializeAsync)}", cancel),
				ConfirmedStore.InitializeAsync($"{nameof(ConfirmedStore)}.{nameof(ConfirmedStore.InitializeAsync)}", cancel)
			};

			await Task.WhenAll(initTasks).ConfigureAwait(false);
			EnsureConsistency();
		}
	}

	#endregion Initializers

	#region Modifiers

	private void AddOrUpdateNoLock(SmartTransaction tx)
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

	public void AddOrUpdate(SmartTransaction tx)
	{
		lock (Lock)
		{
			AddOrUpdateNoLock(tx);
		}
	}

	public bool TryUpdate(SmartTransaction tx)
	{
		var hash = tx.GetHash();
		lock (Lock)
		{
			// Do Contains first, because it's fast.
			if (ConfirmedStore.TryUpdate(tx))
			{
				return true;
			}
			else if (tx.Confirmed && MempoolStore.TryRemove(hash, out var originalTx))
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
			var mempoolTransactions = MempoolStore.GetTransactionHashes();
			foreach (var hash in mempoolTransactions)
			{
				// Contains is fast, so do this first.
				if (ConfirmedStore.Contains(hash)
					&& MempoolStore.TryRemove(hash, out var uTx))
				{
					ConfirmedStore.TryAddOrUpdate(uTx);
				}
			}
		}
	}

	#endregion Modifiers

	#region Accessors

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

	public IEnumerable<uint256> GetTransactionHashes()
	{
		lock (Lock)
		{
			return ConfirmedStore.GetTransactionHashes().Concat(MempoolStore.GetTransactionHashes()).ToList();
		}
	}

	public bool IsEmpty()
	{
		lock (Lock)
		{
			return ConfirmedStore.IsEmpty() && MempoolStore.IsEmpty();
		}
	}

	public bool Contains(uint256 hash)
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
					if (MempoolStore.TryAddOrUpdate(removedTx).isAdded)
					{
						reorgedTxs.Add(removedTx);
					}
				}
			}
			return reorgedTxs;
		}
	}

	/// <returns>Labels ordered by blockchain.</returns>
	public IEnumerable<SmartLabel> GetLabels() => GetTransactions().Select(x => x.Label);

	#endregion Accessors

	public async ValueTask DisposeAsync()
	{
		await MempoolStore.DisposeAsync().ConfigureAwait(false);
		await ConfirmedStore.DisposeAsync().ConfigureAwait(false);
	}
}
