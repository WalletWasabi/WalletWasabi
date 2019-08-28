using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Nito.AsyncEx;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
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

			MempoolService = new MempoolService(this);
			MempoolBehavior = new MempoolBehavior(this);
		}

		public (bool isHashAdded, bool isTxAdded) TryAdd(SmartTransaction tx)
		{
			lock (MempoolLock)
			{
				var isTxAdded = Transactions.TryAdd(tx.GetHash(), tx);
				var isHashAdded = Hashes.Add(tx.GetHash());

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

		public (bool isHashRemoved, bool isTxRemoved) TryRemove(uint256 hash, out SmartTransaction stx)
		{
			lock (MempoolLock)
			{
				var isTxRemoved = Transactions.Remove(hash, out stx);
				var isHashRemoved = Hashes.Remove(hash);

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

				return removed;
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
