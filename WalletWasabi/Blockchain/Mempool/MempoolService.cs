using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Cache;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Mempool;

public class MempoolService
{
	public event EventHandler<SmartTransaction>? TransactionReceived;

	private readonly MemoryCache<uint256, bool> _cache = new(TimeSpan.FromMinutes(30));

	/// <summary>Transactions that we would reply to INV messages.</summary>
	/// <remarks>Guarded by <see cref="_broadcastStoreLock"/>.</remarks>
	private readonly List<TransactionBroadcastEntry> _broadcastStore = new();

	/// <summary>Guards <see cref="_broadcastStore"/>.</summary>
	private readonly object _broadcastStoreLock = new();

	public bool TrustedNodeMode { get; set; }

	public bool TryAddToBroadcastStore(SmartTransaction transaction)
	{
		lock (_broadcastStoreLock)
		{
			if (_broadcastStore.Any(x => x.Is(transaction.Transaction.GetWitHash())))
			{
				return false;
			}

			var entry = new TransactionBroadcastEntry(transaction);
			_broadcastStore.Add(entry);
			return true;
		}
	}

	public bool TryGetFromBroadcastStore(uint256 transactionHash, [NotNullWhen(true)] out TransactionBroadcastEntry? entry)
	{
		lock (_broadcastStoreLock)
		{
			entry = _broadcastStore.FirstOrDefault(x => x.Is(transactionHash));
			return entry is not null;
		}
	}

	public LabelsArray TryGetLabel(uint256 txid)
	{
		var label = LabelsArray.Empty;
		if (TryGetFromBroadcastStore(txid, out var entry))
		{
			label = entry.Transaction.Labels;
		}

		return label;
	}

	public bool IsProcessed(uint256 txid)
	{
		return _cache.TryGet(txid, out _);
	}

	public void Process(Transaction tx)
	{
		var txId = tx.GetHash();
		if(_cache.TryAdd(txId, true, TimeSpan.FromHours(1)))
		{
			var txAdded = new SmartTransaction(tx, Height.Mempool, labels: TryGetLabel(txId));
			TransactionReceived?.Invoke(this, txAdded);
		}
	}

	public bool TrySpend(SmartCoin coin, SmartTransaction tx)
	{
		var spent = false;
		lock (_broadcastStoreLock)
		{
			foreach (var foundCoin in _broadcastStore
				.SelectMany(x => x.Transaction.WalletInputs)
				.Concat(_broadcastStore.SelectMany(x => x.Transaction.WalletOutputs))
				.Where(x => x == coin))
			{
				foundCoin.SpenderTransaction = tx;
				spent = true;
			}
		}
		return spent;
	}
}
