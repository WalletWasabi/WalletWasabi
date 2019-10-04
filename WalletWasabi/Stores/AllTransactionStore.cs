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
using WalletWasabi.Transactions;

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
				var networkIndependentTransactionsFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Transactions");
				if (Directory.Exists(networkIndependentTransactionsFolderPath))
				{
					var oldTransactionsFolderPath = Path.Combine(networkIndependentTransactionsFolderPath, Network.Name);
					if (Directory.Exists(oldTransactionsFolderPath))
					{
						lock (Lock)
						{
							IEnumerable<SmartTransaction> allTransactions = Enumerable.Empty<SmartTransaction>();
							foreach (var filePath in Directory.EnumerateFiles(oldTransactionsFolderPath))
							{
								try
								{
									string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
									var allWalletTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.OrderByBlockchain() ?? Enumerable.Empty<SmartTransaction>();
									allTransactions = allTransactions.Concat(allWalletTransactions);

									// ToDo: Uncomment when PR is finished.
									// File.Delete(filePath);
								}
								catch (Exception ex)
								{
									Logger.LogTrace(ex);
								}
							}

							AddOrUpdateNoLock(allTransactions.ToList());

							// ToDo: Uncomment when PR is finished.
							// Directory.Delete(oldTransactionsFolderPath, recursive: true);
						}
					}

					// ToDo: Uncomment when PR is finished.
					//// If all networks successfully migrated, too, then delete the transactions folder, too.
					//if (!Directory.EnumerateFileSystemEntries(networkIndependentTransactionsFolderPath).Any())
					//{
					//	Directory.Delete(networkIndependentTransactionsFolderPath, recursive: true);
					//}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Backwards compatibility could not be ensured.");
				Logger.LogWarning(ex);
			}
		}

		private void AddOrUpdateNoLock(SmartTransaction tx)
		{
			var hash = tx.GetHash();

			if (tx.Confirmed)
			{
				if (MempoolStore.TryRemove(hash, out SmartTransaction found))
				{
					found.Update(tx, forceHeightUpdate: false);
					ConfirmedStore.TryAdd(found);
				}
				else
				{
					ConfirmedStore.TryAdd(tx);
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

		private bool UpdateNoLock(SmartTransaction tx)
		{
			var hash = tx.GetHash();

			if (tx.Confirmed)
			{
				if (MempoolStore.TryRemove(hash, out SmartTransaction found))
				{
					found.Update(tx, forceHeightUpdate: false);
					if (ConfirmedStore.TryAdd(found))
					{
						return true;
					}
				}
			}

			return false;
		}

		public bool Update(SmartTransaction tx)
		{
			lock (Lock)
			{
				return UpdateNoLock(tx);
			}
		}

		public void AddOrUpdate(SmartTransaction tx)
		{
			lock (Lock)
			{
				AddOrUpdateNoLock(tx);
			}
		}

		private void AddOrUpdateNoLock(IEnumerable<SmartTransaction> txs)
		{
			var toAddConfirmedStore = new HashSet<SmartTransaction>();
			var toAddMempoolStore = new HashSet<SmartTransaction>();

			foreach (var tx in txs.OrderByBlockchain())
			{
				var hash = tx.GetHash();

				if (tx.Confirmed)
				{
					if (MempoolStore.TryRemove(hash, out SmartTransaction found))
					{
						found.Update(tx, forceHeightUpdate: false);
						toAddConfirmedStore.Add(found);
					}
					else
					{
						toAddConfirmedStore.Add(tx);
					}
				}
				else
				{
					if (!ConfirmedStore.Contains(hash))
					{
						toAddMempoolStore.Add(tx);
					}
				}
			}

			ConfirmedStore.TryAdd(toAddConfirmedStore);
			MempoolStore.TryAdd(toAddMempoolStore.Except(toAddConfirmedStore));
		}

		public void AddOrUpdate(IEnumerable<SmartTransaction> txs)
		{
			lock (Lock)
			{
				AddOrUpdateNoLock(txs);
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
