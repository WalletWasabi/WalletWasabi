using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	public class MempoolCache : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private bool _isEmpty;

		/// <summary>
		/// All the transaction hashes in our mempool.
		/// </summary>
		private HashSet<uint256> MempoolHashes { get; }

		/// <summary>
		/// All the mempool transactions those a wallet may need.
		/// </summary>
		private SortedSet<SmartTransaction> MempoolTransactions { get; }

		private object Lock { get; }

		public bool IsEmpty
		{
			get => _isEmpty;
			private set
			{
				if (_isEmpty != value)
				{
					_isEmpty = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
				}
			}
		}

		public MempoolCache()
		{
			MempoolHashes = new HashSet<uint256>();
			MempoolTransactions = new SortedSet<SmartTransaction>(SmartTransaction.GetBlockchainComparer());
			Lock = new object();

			IsEmpty = true;
		}

		public bool TryAddHashes(params uint256[] txids)
		{
			if (txids is null || !txids.Any())
			{
				return false;
			}

			var added = false;
			lock (Lock)
			{
				foreach (var txid in txids)
				{
					if (MempoolHashes.Add(txid))
					{
						added = true;
					}
				}

				if (added)
				{
					IsEmpty = false;
				}
			}
			return added;
		}

		public bool TryAddHashesAndTransactions(params SmartTransaction[] txs)
		{
			if (txs is null || !txs.Any())
			{
				return false;
			}

			var added = false;
			lock (Lock)
			{
				foreach (var tx in txs)
				{
					if (MempoolHashes.Add(tx.GetHash()))
					{
						added = true;
					}

					if (MempoolTransactions.Add(tx))
					{
						added = true;
					}

					if (added)
					{
						IsEmpty = false;
					}
				}
			}
			return added;
		}

		public bool SetEquals(params SmartTransaction[] txs)
		{
			if (txs is null)
			{
				txs = new SmartTransaction[0];
			}

			lock (Lock)
			{
				return MempoolTransactions.SetEquals(txs);
			}
		}

		public string MempoolTransactionsToJson()
		{
			lock (Lock)
			{
				return JsonConvert.SerializeObject(MempoolTransactions, Formatting.Indented);
			}
		}

		public static IOrderedEnumerable<SmartTransaction> MempoolTransactionsFromJson(string jsonString, params uint256[] except)
			=> JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?
				.Where(x => !x.Confirmed && !except.Contains(x.GetHash()))?
				.OrderByBlockchain();

		public (int removedHashCount, IEnumerable<uint256> removedTxs) Cleanup(ISet<string> allMempoolHashes, int compactness)
		{
			if (allMempoolHashes is null || !allMempoolHashes.Any())
			{
				return (0, Enumerable.Empty<uint256>());
			}

			int removedHashCount = 0;
			HashSet<uint256> removedTxs = new HashSet<uint256>();

			lock (Lock)
			{
				if (IsEmpty)
				{
					return (0, Enumerable.Empty<uint256>());
				}

				IEnumerable<uint256> toRemove = MempoolHashes.Where(x => !allMempoolHashes.Contains(x.ToString().Substring(0, compactness))).
					ToArray(); // Because we use this to modify collection.

				foreach (uint256 tx in toRemove)
				{
					if (MempoolHashes.Remove(tx))
					{
						removedHashCount++;
					}

					if (MempoolTransactions.RemoveWhere(x => x.GetHash() == tx) > 0)
					{
						removedTxs.Add(tx);
					}
				}

				IsEmpty = MempoolHashes.Count == 0;
			}

			return (removedHashCount, removedTxs);
		}

		public (int removedHashCount, IEnumerable<uint256> removedTxs) Remove(params uint256[] toRemove)
		{
			if (toRemove is null || !toRemove.Any())
			{
				return (0, Enumerable.Empty<uint256>());
			}

			int removedHashCount = 0;
			HashSet<uint256> removedTxs = new HashSet<uint256>();
			lock (Lock)
			{
				if (IsEmpty)
				{
					return (0, Enumerable.Empty<uint256>());
				}

				foreach (uint256 tx in toRemove)
				{
					if (MempoolHashes.Remove(tx))
					{
						removedHashCount++;
					}

					if (MempoolTransactions.RemoveWhere(x => x.GetHash() == tx) > 0)
					{
						removedTxs.Add(tx);
					}
				}

				IsEmpty = MempoolHashes.Count == 0;
			}

			return (removedHashCount, removedTxs);
		}

		public (bool removedHash, bool removedTx) Confirm(uint256 toConfirm, Height height, uint256 blockHash, int blockIndex)
		{
			if (toConfirm is null)
			{
				return (false, false);
			}

			bool removedHash = false;
			bool removedTx = false;
			lock (Lock)
			{
				if (IsEmpty)
				{
					return (false, false);
				}

				if (MempoolHashes.Remove(toConfirm))
				{
					removedHash = true;
				}

				var stx = MempoolTransactions.FirstOrDefault(x => x.GetHash() == toConfirm);
				if (!(stx is null))
				{
					MempoolTransactions.Remove(stx);
					stx.SetHeight(height, blockHash, blockIndex);
					removedTx = true;
				}

				IsEmpty = MempoolHashes.Count == 0;
			}
			return (removedHash, removedTx);
		}

		public SmartTransaction[] GetTransactions()
		{
			lock (Lock)
			{
				return MempoolTransactions.ToArray();
			}
		}
	}
}
