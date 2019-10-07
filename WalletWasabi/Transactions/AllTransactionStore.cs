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

namespace WalletWasabi.Transactions
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
							foreach (var filePath in Directory.EnumerateFiles(oldTransactionsFolderPath))
							{
								try
								{
									string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
									var allWalletTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.OrderByBlockchain() ?? Enumerable.Empty<SmartTransaction>();
									foreach (var tx in allWalletTransactions)
									{
										TryAddNoLock(tx);
									}

									// ToDo: Uncomment when PR is finished.
									// File.Delete(filePath);
								}
								catch (Exception ex)
								{
									Logger.LogTrace(ex);
								}
							}

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

		private void TryAddNoLock(SmartTransaction tx)
		{
			var hash = tx.GetHash();

			if (tx.Confirmed)
			{
				if (MempoolStore.TryRemove(hash, out SmartTransaction found))
				{
					found.Update(tx);
					ConfirmedStore.TryAdd(found);
				}
				else
				{
					ConfirmedStore.TryAdd(tx);
				}
			}
			else
			{
				if (ConfirmedStore.TryGetTransaction(hash, out SmartTransaction found))
				{
					found.Update(tx);
				}
				else
				{
					MempoolStore.TryAdd(tx);
				}
			}
		}

		public void TryAdd(SmartTransaction tx)
		{
			lock (Lock)
			{
				TryAddNoLock(tx);
			}
		}

		public bool TryUpdate(SmartTransaction tx, out SmartTransaction originalTx)
		{
			var hash = tx.GetHash();
			lock (Lock)
			{
				// Do Contains first, because it's fast.
				if (ConfirmedStore.Contains(hash) && ConfirmedStore.TryGetTransaction(hash, out originalTx))
				{
					originalTx.Update(tx);
					return true;
				}
				else if (MempoolStore.Contains(hash) && MempoolStore.TryGetTransaction(hash, out originalTx))
				{
					originalTx.Update(tx);
					return true;
				}
			}

			originalTx = null;
			return false;
		}

		public bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
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
	}
}
