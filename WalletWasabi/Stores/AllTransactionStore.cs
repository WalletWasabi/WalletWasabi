using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	public class AllTransactionStore
	{
		private string WorkFolderPath { get; set; }
		private Network Network { get; set; }

		public TransactionStore MempoolStore { get; private set; }
		public TransactionStore ConfirmedStore { get; private set; }
		private object Lock { get; set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				Network = Guard.NotNull(nameof(network), network);

				MempoolStore = new TransactionStore();
				ConfirmedStore = new TransactionStore();
				Lock = new object();

				var mempoolWorkFolder = Path.Combine(WorkFolderPath, "Mempool");
				var confirmedWorkFolder = Path.Combine(WorkFolderPath, "ConfirmedTransactions");

				var initTasks = new[]
				{
					MempoolStore.InitializeAsync(mempoolWorkFolder, Network, $"{nameof(MempoolStore)}.{nameof(MempoolStore.InitializeAsync)}"),
					ConfirmedStore.InitializeAsync(confirmedWorkFolder, Network, $"{nameof(ConfirmedStore)}.{nameof(ConfirmedStore.InitializeAsync)}")
				};

				await Task.WhenAll(initTasks).ConfigureAwait(false);

				TryEnsureBackwardsCompatibility();
			}
		}

		private void TryEnsureBackwardsCompatibility()
		{
			try
			{
				// Before Wasabi 1.1.7
				var oldTransactionsFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Transactions", Network.Name);
				if (Directory.Exists(oldTransactionsFolderPath))
				{
					lock (Lock)
					{
						foreach (var filePath in Directory.EnumerateFiles(oldTransactionsFolderPath))
						{
							try
							{
								string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
								var allTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.OrderByBlockchain() ?? Enumerable.Empty<SmartTransaction>();
								AddOrUpdateNoLock(allTransactions);

								// ToDo: Uncomment when PR is finished.
								// File.Delete(filePath);
							}
							catch (Exception ex)
							{
								Logger.LogTrace(ex);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Backwards compatibility could not be ensured.");
				Logger.LogWarning(ex);
			}
		}

		/// <param name="isReorg">The tx cannot unconfirm by default, only if reorg is explicitly specified.</param>
		private void AddOrUpdateNoLock(SmartTransaction tx, bool isReorg = false)
		{
			var hash = tx.GetHash();

			if (tx.Confirmed)
			{
				if (MempoolStore.TryRemove(hash, out SmartTransaction found))
				{
					found.SetHeight(tx.Height, tx.BlockHash, tx.BlockIndex);
					ConfirmedStore.TryAdd(found);
				}
				else
				{
					ConfirmedStore.TryAdd(tx);
				}
			}
			else if (isReorg)
			{
				if (ConfirmedStore.TryRemove(hash, out SmartTransaction found))
				{
					found.SetHeight(Height.Mempool);
					MempoolStore.TryAdd(found);
				}
				else
				{
					MempoolStore.TryAdd(tx);
				}
			}
			else
			{
				if (!ConfirmedStore.Contains(hash))
				{
					MempoolStore.TryAdd(tx);
				}
			}
		}

		/// <param name="isReorg">The tx cannot unconfirm by default, only if reorg is explicitly specified.</param>
		private bool UpdateNoLock(SmartTransaction tx, bool isReorg = false)
		{
			var hash = tx.GetHash();

			if (tx.Confirmed)
			{
				if (MempoolStore.TryRemove(hash, out SmartTransaction found))
				{
					found.SetHeight(tx.Height, tx.BlockHash, tx.BlockIndex);
					if (ConfirmedStore.TryAdd(found))
					{
						return true;
					}
				}
			}
			else if (isReorg && ConfirmedStore.TryRemove(hash, out SmartTransaction found))
			{
				found.SetHeight(Height.Mempool);
				if (MempoolStore.TryAdd(found))
				{
					return true;
				}
			}

			return false;
		}

		/// <param name="isReorg">The tx cannot unconfirm by default, only if reorg is explicitly specified.</param>
		public bool Update(SmartTransaction tx, bool isReorg = false)
		{
			lock (Lock)
			{
				return UpdateNoLock(tx, isReorg);
			}
		}

		/// <param name="isReorg">The tx cannot unconfirm by default, only if reorg is explicitly specified.</param>
		public void AddOrUpdate(SmartTransaction tx, bool isReorg = false)
		{
			lock (Lock)
			{
				AddOrUpdateNoLock(tx, isReorg);
			}
		}

		/// <param name="isReorg">The tx cannot unconfirm by default, only if reorg is explicitly specified.</param>
		private void AddOrUpdateNoLock(IEnumerable<SmartTransaction> txs, bool isReorg = false)
		{
			foreach (var tx in txs)
			{
				AddOrUpdateNoLock(tx, isReorg);
			}
		}

		/// <param name="isReorg">The tx cannot unconfirm by default, only if reorg is explicitly specified.</param>
		public void AddOrUpdate(IEnumerable<SmartTransaction> txs, bool isReorg = false)
		{
			lock (Lock)
			{
				foreach (var tx in txs)
				{
					AddOrUpdateNoLock(tx, isReorg);
				}
			}
		}

		public bool TryGetTransaction(uint256 hash, out SmartTransaction tx)
		{
			lock (Lock)
			{
				if (MempoolStore.TryGetTransaction(hash, out tx))
				{
					return true;
				}

				return ConfirmedStore.TryGetTransaction(hash, out tx);
			}
		}
	}
}
