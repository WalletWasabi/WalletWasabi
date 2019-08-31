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

namespace WalletWasabi.Stores.Transactions
{
	public class MempoolStore : IStore
	{
		public string WorkFolderPath => TransactionStore.WorkFolderPath;
		public Network Network => TransactionStore.Network;
		public MempoolService MempoolService { get; private set; }
		public MempoolBehavior MempoolBehavior { get; private set; }

		private TransactionStore TransactionStore { get; set; }
		private HashSet<uint256> Hashes { get; set; }
		private object MempoolLock { get; set; }

		public MempoolStore()
		{
		}

		public async Task InitializeAsync(string workFolderPath, Network network, bool ensureBackwardsCompatibility)
		{
			var initStart = DateTimeOffset.UtcNow;

			Hashes = new HashSet<uint256>();
			TransactionStore = new TransactionStore("Mempool.dat", () => TryEnsureBackwardsCompatibility(), clearOnRegtest: true);
			MempoolLock = new object();

			await TransactionStore.InitializeAsync(workFolderPath, network, ensureBackwardsCompatibility);

			lock (MempoolLock)
			{
				foreach (var tx in TransactionStore.GetTransactions())
				{
					Hashes.Add(tx.GetHash());
				}
			}

			MempoolService = new MempoolService(this);
			MempoolBehavior = new MempoolBehavior(this);

			var elapsedSeconds = Math.Round((DateTimeOffset.UtcNow - initStart).TotalSeconds, 1);
			Logger.LogInfo<MempoolStore>($"Initialized in {elapsedSeconds} seconds.");
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
								TryAddNoLockNoSerialization(unconfirmedTransactions);
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

		private ISet<SmartTransaction> TryAddNoLockNoSerialization(IEnumerable<SmartTransaction> transactions)
		{
			transactions = transactions ?? Enumerable.Empty<SmartTransaction>();
			var added = new HashSet<SmartTransaction>();
			foreach (var tx in transactions)
			{
				if (TryAddNoLockNoSerialization(tx).isTxAdded)
				{
					added.Add(tx);
				}
			}

			return added;
		}

		public (bool isHashAdded, bool isTxAdded) TryAdd(SmartTransaction tx)
		{
			lock (MempoolLock)
			{
				return TryAddNoLockNoSerialization(tx);
			}
		}

		private (bool isHashAdded, bool isTxAdded) TryAddNoLockNoSerialization(SmartTransaction tx)
		{
			var isTxAdded = TransactionStore.TryAdd(tx);
			var isHashAdded = Hashes.Add(tx.GetHash());
			return (isHashAdded, isTxAdded);
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
				var isTxRemoved = TransactionStore.TryRemove(hash, out stx);
				var isHashRemoved = Hashes.Remove(hash);

				return (isHashRemoved, isTxRemoved);
			}
		}

		public ISet<uint256> RemoveExcept(ISet<string> allCompactHashes, int compactness)
		{
			lock (MempoolLock)
			{
				var toRemoveHashes = Hashes.Where(x => !allCompactHashes.Contains(x.ToString().Substring(0, compactness))).ToHashSet();

				var removedHashes = new HashSet<uint256>();
				foreach (var remove in toRemoveHashes)
				{
					if (Hashes.Remove(remove))
					{
						removedHashes.Add(remove);
					}
				}

				TransactionStore.TryRemove(removedHashes);

				return removedHashes;
			}
		}

		public bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
		{
			lock (MempoolLock)
			{
				return TransactionStore.TryGetTransaction(hash, out sameStx);
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
				return TransactionStore.GetTransactions();
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
