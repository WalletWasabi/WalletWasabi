using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Models;

namespace WalletWasabi.Stores.Mempool
{
	public class MempoolStore : IStore
	{
		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }

		private Dictionary<uint256, SmartTransaction> Transactions { get; set; }
		private HashSet<uint256> Hashes { get; set; }

		public MempoolStore()
		{
		}

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
			Hashes = new HashSet<uint256>();
			Transactions = new Dictionary<uint256, SmartTransaction>();
		}

		public void Add(SmartTransaction tx)
		{
			Transactions.Add(tx.GetHash(), tx);
			Hashes.Add(tx.GetHash());
		}

		public void Add(uint256 hash)
		{
			Hashes.Add(hash);
		}

		public bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
		{
			return Transactions.TryGetValue(hash, out sameStx);
		}

		public bool ContainsHash(uint256 hash)
		{
			return Hashes.Contains(hash);
		}

		public void Remove(uint256 hash)
		{
			Transactions.Remove(hash);
			Hashes.Remove(hash);
		}

		public ISet<SmartTransaction> GetTransactions()
		{
			return Transactions.Values.ToHashSet();
		}

		public ISet<uint256> GetTransactionHashes()
		{
			return Hashes.ToHashSet();
		}
	}
}
