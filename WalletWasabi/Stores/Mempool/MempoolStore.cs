using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Nito.AsyncEx;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Stores.Mempool
{
	public class MempoolStore : IStore
	{
		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }
		public MempoolService MempoolService { get; private set; }
		public MempoolBehavior MempoolBehavior { get; private set; }

		private Dictionary<uint256, SmartTransaction> Transactions { get; set; }
		private HashSet<uint256> Hashes { get; set; }
		private object MempoolLock { get; set; }
		private MutexIoManager MempoolFileManager { get; set; }

		public MempoolStore()
		{
		}

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			Hashes = new HashSet<uint256>();
			Transactions = new Dictionary<uint256, SmartTransaction>();
			MempoolLock = new object();

			var mempoolFilePath = Path.Combine(WorkFolderPath, "Mempool.dat");
			MempoolFileManager = new MutexIoManager(mempoolFilePath);

			using (await MempoolFileManager.Mutex.LockAsync())
			{
				IoHelpers.EnsureDirectoryExists(WorkFolderPath);

				TryEnsureBackwardsCompatibility();

				if (Network == Network.RegTest)
				{
					MempoolFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
				}

				if (!MempoolFileManager.Exists())
				{
					await SerializeAllTransactionsAsync();
				}

				await InitializeTransactionsAsync();
			}

			MempoolService = new MempoolService(this);
			MempoolBehavior = new MempoolBehavior(this);
		}

		private async Task SerializeAllTransactionsAsync()
		{
			List<SmartTransaction> transactionsClone;
			lock (MempoolLock)
			{
				transactionsClone = Transactions.Values.ToList();
			}

			await MempoolFileManager.WriteAllLinesAsync(transactionsClone.OrderByBlockchain().Select(x => x.ToLine()));
		}

		private async Task InitializeTransactionsAsync()
		{
			try
			{
				IoHelpers.EnsureFileExists(MempoolFileManager.FilePath);
				var allLines = await MempoolFileManager.ReadAllLinesAsync();
				var allTransactions = allLines.Select(x => SmartTransaction.FromLine(x, Network));
				lock (MempoolLock)
				{
					foreach (var tx in allTransactions)
					{
						if (Transactions.TryAdd(tx.GetHash(), tx))
						{
							Hashes.Add(tx.GetHash());
						}
					}
				}

				if (allTransactions.Count() != Transactions.Count)
				{
					// Another process worked into the file and appended the same transaction into it.
					// In this case we correct the file by serializing the unique set.
					await SerializeAllTransactionsAsync();
				}
			}
			catch
			{
				// We found a corrupted entry. Stop here.
				// Delete the currupted file.
				// Do not try to autocorrect, because the internal data structures are throwing events that may confuse the consumers of those events.
				Logger.LogError<MempoolStore>("Mempool file got corrupted. Deleting it...");
				MempoolFileManager.DeleteMe();
				throw;
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
					foreach (var filePath in Directory.EnumerateFiles(oldTransactionsFolderPath))
					{
						try
						{
							string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
							var unconfirmedTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.Where(x => !x.Confirmed)?.OrderByBlockchain() ?? Enumerable.Empty<SmartTransaction>();
							lock (MempoolLock)
							{
								foreach (var tx in unconfirmedTransactions)
								{
									if (Transactions.TryAdd(tx.GetHash(), tx))
									{
										Hashes.Add(tx.GetHash());
									}
								}
							}
						}
						catch (Exception ex)
						{
							Logger.LogTrace<MempoolStore>(ex);
						}
						// Do not delete, because it's still used for confirmed transaction cache.
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<MempoolStore>($"Backwards compatibility could not be ensured. Exception: {ex}.");
			}
		}

		public (bool isHashAdded, bool isTxAdded) TryAdd(SmartTransaction tx)
		{
			lock (MempoolLock)
			{
				var isTxAdded = Transactions.TryAdd(tx.GetHash(), tx);
				var isHashAdded = Hashes.Add(tx.GetHash());

				if (isTxAdded)
				{
					_ = TryAppendToFileAsync(tx);
				}

				return (isHashAdded, isTxAdded);
			}
		}

		public bool TryAdd(uint256 hash)
		{
			lock (MempoolLock)
			{
				return Hashes.Add(hash);
			}
		}

		public (bool isHashRemoved, bool isTxRemoved) TryRemove(uint256 hash, out SmartTransaction stx)
		{
			lock (MempoolLock)
			{
				var isTxRemoved = Transactions.Remove(hash, out stx);
				var isHashRemoved = Hashes.Remove(hash);

				if (isTxRemoved)
				{
					_ = TryRemoveFromFileAsync(hash);
				}

				return (isHashRemoved, isTxRemoved);
			}
		}

		public ISet<uint256> RemoveExcept(ISet<string> allCompactHashes, int compactness)
		{
			lock (MempoolLock)
			{
				var toRemoveHashes = Hashes.Where(x => !allCompactHashes.Contains(x.ToString().Substring(0, compactness))).ToHashSet();
				var removed = new HashSet<uint256>();

				foreach (uint256 hash in toRemoveHashes)
				{
					if (Hashes.Remove(hash))
					{
						Transactions.Remove(hash);
						removed.Add(hash);
					}
				}

				_ = TryRemoveFromFileAsync(removed.ToArray());

				return removed;
			}
		}

		private async Task TryAppendToFileAsync(SmartTransaction stx)
		{
			try
			{
				using (await MempoolFileManager.Mutex.LockAsync())
				{
					try
					{
						await MempoolFileManager.AppendAllLinesAsync(new[] { stx.ToLine() });
					}
					catch
					{
						await SerializeAllTransactionsAsync();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError<MempoolStore>(ex);
			}
		}

		private async Task TryRemoveFromFileAsync(params uint256[] toRemoves)
		{
			try
			{
				if (toRemoves is null || !toRemoves.Any())
				{
					return;
				}

				using (await MempoolFileManager.Mutex.LockAsync())
				{
					string[] allLines = await MempoolFileManager.ReadAllLinesAsync();
					var toSerialize = new List<string>();
					foreach (var line in allLines)
					{
						var startsWith = false;
						foreach (var toRemoveString in toRemoves.Select(x => x.ToString()))
						{
							startsWith = startsWith || line.StartsWith(toRemoveString, StringComparison.Ordinal);
						}

						if (!startsWith)
						{
							toSerialize.Add(line);
						}
					}

					try
					{
						await MempoolFileManager.WriteAllLinesAsync(toSerialize);
					}
					catch
					{
						await SerializeAllTransactionsAsync();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError<MempoolStore>(ex);
			}
		}

		public bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
		{
			lock (MempoolLock)
			{
				return Transactions.TryGetValue(hash, out sameStx);
			}
		}

		public bool ContainsHash(uint256 hash)
		{
			lock (MempoolLock)
			{
				return Hashes.Contains(hash);
			}
		}

		public ISet<SmartTransaction> GetTransactions()
		{
			lock (MempoolLock)
			{
				return Transactions.Values.ToHashSet();
			}
		}

		public ISet<uint256> GetTransactionHashes()
		{
			lock (MempoolLock)
			{
				return Hashes.ToHashSet();
			}
		}

		public bool IsEmpty()
		{
			lock (MempoolLock)
			{
				return !Hashes.Any();
			}
		}
	}
}
