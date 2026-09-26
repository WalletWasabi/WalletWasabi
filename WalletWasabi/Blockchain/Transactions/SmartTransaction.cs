using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

[DebuggerDisplay("{Transaction.GetHash()}")]
public class SmartTransaction : IEquatable<SmartTransaction>
{
	private Lazy<long[]> _outputValues;
	private Lazy<bool> _isWasabi2Cj;

	public SmartTransaction(
		Transaction transaction,
		Height? height = null,
		uint256? blockHash = null,
		int blockIndex = 0, // FIXME: unconfirmed/unknown txs are not in the genesis block
		LabelsArray? labels = null,
		bool isReplacement = false,
		bool isSpeedup = false,
		bool isCancellation = false,
		DateTimeOffset firstSeen = default)
	{
		Transaction = transaction;

		// Because we don't modify those transactions, we can cache the hash
		Transaction.PrecomputeHash(false, true);

		_labels = labels ?? LabelsArray.Empty;

		_height = height ?? Unknown;
		_blockHash = blockHash;
		_blockIndex = blockIndex;

		_firstSeen = firstSeen == default ? DateTimeOffset.UtcNow : firstSeen;

		_isReplacement = isReplacement;
		_isSpeedup = isSpeedup;
		_isCancellation = isCancellation;
		_walletInputsInternal = new HashSet<SmartCoin>(Transaction.Inputs.Count);
		_walletOutputsInternal = new HashSet<SmartCoin>(Transaction.Outputs.Count);

		_outputValues = new Lazy<long[]>(() => Transaction.Outputs.Select(x => x.Value.Satoshi).ToArray(), true);
		_isWasabi2Cj = new Lazy<bool>(
			() => Transaction.Outputs.Count >= 2 // Sanity check.
			&& Transaction.Inputs.Count >= 21 // 21 is the absolute minimum input count
			&& OutputValues.Count(x => BlockchainAnalyzer.StdDenoms.Contains(x)) > OutputValues.Length * 0.8 // Most of the outputs contains the denomination.
			&& OutputValues.Zip(OutputValues.Skip(1)).All(p => p.First >= p.Second), // Outputs are ordered descending.
			isThreadSafe: true);
	}

	#region Members

	public long[] OutputValues => _outputValues.Value;
	public bool IsWasabi2Cj => _isWasabi2Cj.Value;

	/// <summary>
	/// Guards the wallet coin sets, their snapshots and the derived caches.
	/// Nothing is called out of this class while holding it, so it is always the innermost lock.
	/// </summary>
	private readonly object _stateLock = new();

	/// <summary>Coins those are on the input side of the tx and belong to ANY loaded wallet. Later if more wallets are loaded this list can increase.</summary>
	private readonly HashSet<SmartCoin> _walletInputsInternal;

	/// <summary>Coins those are on the output side of the tx and belong to ANY loaded wallet. Later if more wallets are loaded this list can increase.</summary>
	private readonly HashSet<SmartCoin> _walletOutputsInternal;

	/// <summary>Cached computation of <see cref="ForeignInputs"/> or <c>null</c> when re-computation is needed.</summary>
	private HashSet<IndexedTxIn>? ForeignInputsCache { get; set; } = null;

	/// <summary>Cached computation of <see cref="ForeignOutputs"/> or <c>null</c> when re-computation is needed.</summary>
	private HashSet<IndexedTxOut>? ForeignOutputsCache { get; set; } = null;

	/// <summary>Cached computation of <see cref="WalletVirtualInputs"/> or <c>null</c> when re-computation is needed.</summary>
	private HashSet<WalletVirtualInput>? WalletVirtualInputsCache { get; set; } = null;

	/// <summary>Cached computation of <see cref="WalletVirtualOutputs"/> or <c>null</c> when re-computation is needed.</summary>
	private HashSet<WalletVirtualOutput>? WalletVirtualOutputsCache { get; set; } = null;

	/// <summary>Cached computation of <see cref="ForeignVirtualOutputs"/> or <c>null</c> when re-computation is needed.</summary>
	private HashSet<ForeignVirtualOutput>? ForeignVirtualOutputsCache { get; set; } = null;

	/// <summary>Snapshot of <see cref="_walletInputsInternal"/> handed out to readers or <c>null</c> when it needs to be re-created.</summary>
	private SmartCoin[]? _walletInputsSnapshot;

	/// <summary>Snapshot of <see cref="_walletOutputsInternal"/> handed out to readers or <c>null</c> when it needs to be re-created.</summary>
	private SmartCoin[]? _walletOutputsSnapshot;

	public IReadOnlyCollection<SmartCoin> WalletInputs
	{
		get
		{
			lock (_stateLock)
			{
				return _walletInputsSnapshot ??= _walletInputsInternal.ToArray();
			}
		}
	}

	public IReadOnlyCollection<SmartCoin> WalletOutputs
	{
		get
		{
			lock (_stateLock)
			{
				return _walletOutputsSnapshot ??= _walletOutputsInternal.ToArray();
			}
		}
	}

	public IReadOnlyCollection<IndexedTxIn> ForeignInputs
	{
		get
		{
			lock (_stateLock)
			{
				if (ForeignInputsCache is null)
				{
					var walletInputOutpoints = _walletInputsInternal.Select(smartCoin => smartCoin.Outpoint).ToHashSet();
					ForeignInputsCache = Transaction.Inputs.AsIndexedInputs().Where(i => !walletInputOutpoints.Contains(i.PrevOut)).ToHashSet();
				}
				return ForeignInputsCache;
			}
		}
	}

	public IReadOnlyCollection<IndexedTxOut> ForeignOutputs
	{
		get
		{
			lock (_stateLock)
			{
				if (ForeignOutputsCache is null)
				{
					var walletOutputIndices = _walletOutputsInternal.Select(smartCoin => smartCoin.Outpoint.N).ToHashSet();
					ForeignOutputsCache = Transaction.Outputs.AsIndexedOutputs().Where(o => !walletOutputIndices.Contains(o.N)).ToHashSet();
				}
				return ForeignOutputsCache;
			}
		}
	}

	/// <summary>Wallet inputs with the same script are virtually considered to be the same by blockchain analysis.</summary>
	public IReadOnlyCollection<WalletVirtualInput> WalletVirtualInputs
	{
		get
		{
			lock (_stateLock)
			{
				WalletVirtualInputsCache ??= _walletInputsInternal
					.GroupBy(i => i.HdPubKey.PubKey)
					.Select(g => new WalletVirtualInput(g.Key.ToBytes(), g.ToHashSet()))
					.ToHashSet();
				return WalletVirtualInputsCache;
			}
		}
	}

	/// <summary>Wallet outputs with the same script are virtually considered to be the same by blockchain analysis.</summary>
	public IReadOnlyCollection<WalletVirtualOutput> WalletVirtualOutputs
	{
		get
		{
			lock (_stateLock)
			{
				WalletVirtualOutputsCache ??= _walletOutputsInternal
					.GroupBy(o => o.HdPubKey.PubKey)
					.Select(g => new WalletVirtualOutput(g.Key.ToBytes(), g.ToHashSet()))
					.ToHashSet();
				return WalletVirtualOutputsCache;
			}
		}
	}

	/// <summary>Foreign outputs with the same script are virtually considered to be the same by blockchain analysis.</summary>
	public IReadOnlyCollection<ForeignVirtualOutput> ForeignVirtualOutputs
	{
		get
		{
			lock (_stateLock)
			{
				ForeignVirtualOutputsCache ??= ForeignOutputs
						.GroupBy(o => o.TxOut.ScriptPubKey.ExtractKeyId(), new ByteArrayEqualityComparer())
						.Select(g => new ForeignVirtualOutput(g.Key, g.Sum(o => o.TxOut.Value), g.Select(o => new OutPoint(GetHash(), o.N)).ToHashSet()))
						.ToHashSet();
				return ForeignVirtualOutputsCache;
			}
		}
	}

	public Transaction Transaction { get; }

	// The mutable state below is guarded by _stateLock, because the transaction is shared between the
	// sync, broadcaster, store and UI threads.
	private Height _height;
	private uint256? _blockHash;
	private int _blockIndex;
	private LabelsArray _labels;
	private DateTimeOffset _firstSeen;
	private bool _isReplacement;
	private bool _isSpeedup;
	private bool _isCancellation;

	public Height Height { get { lock (_stateLock) { return _height; } } }

	public uint256? BlockHash { get { lock (_stateLock) { return _blockHash; } } }

	public int BlockIndex { get { lock (_stateLock) { return _blockIndex; } } }

	public LabelsArray Labels
	{
		get { lock (_stateLock) { return _labels; } }
		set { lock (_stateLock) { _labels = value; } }
	}

	public DateTimeOffset FirstSeen { get { lock (_stateLock) { return _firstSeen; } } }

	public bool IsReplacement { get { lock (_stateLock) { return _isReplacement; } } }

	public bool IsSpeedup { get { lock (_stateLock) { return _isSpeedup; } } }

	public bool IsCancellation { get { lock (_stateLock) { return _isCancellation; } } }

	/// <summary>Reads <see cref="Height"/>, <see cref="BlockHash"/> and <see cref="BlockIndex"/> as one consistent snapshot.</summary>
	public (Height Height, uint256? BlockHash, int BlockIndex) GetBlockInfo()
	{
		lock (_stateLock)
		{
			return (_height, _blockHash, _blockIndex);
		}
	}

	/// <summary>Merges <paramref name="labels"/> into <see cref="Labels"/> atomically, so concurrent merges are not lost.</summary>
	public void AddLabels(LabelsArray labels)
	{
		lock (_stateLock)
		{
			_labels = LabelsArray.Merge(_labels, labels);
		}
	}

	public bool IsCPFP => ParentsThisTxPaysFor.Any();
	public bool IsCPFPd => ChildrenPayForThisTx.Any();

	/// <summary>
	/// Children transactions those are paying for this transaction.
	/// </summary>
	public IEnumerable<SmartTransaction> ChildrenPayForThisTx => WalletOutputs
		.Where(x => x.SpenderTransaction is { } spender && spender.IsCPFP && spender.Height == Height)
		.Select(x => x.SpenderTransaction!);

	/// <summary>
	/// Parent transactions this transaction is paying for.
	/// </summary>
	public IEnumerable<SmartTransaction> ParentsThisTxPaysFor =>
		IsSpeedup && !IsCancellation && ForeignInputs.Count == 0 && ForeignOutputs.Count == 0
			? WalletInputs
				.Select(x => x.Transaction)
				.Where(x => x.Height == Height
					|| (x.Height == Height.Mempool && Height == Height.Unknown)) // It's ok if we didn't yet get to the mempool to consider this CPFP.
			: Enumerable.Empty<SmartTransaction>();

	public bool Confirmed => Height is ChainHeight;

	public uint256 GetHash() => Transaction.GetHash();

	public bool IsImmature(ChainHeight bestHeight)
	{
		return Transaction.IsCoinBase && Height is ChainHeight h && h + 100 >= bestHeight;
	}

	#endregion Members

	public IEnumerable<SmartCoin> GetWalletInputs(KeyManager keyManager)
	{
		foreach (var coin in WalletInputs)
		{
			if (keyManager.TryGetKeyForScriptPubKey(coin.ScriptPubKey, out _))
			{
				yield return coin;
			}
		}
	}

	public IEnumerable<SmartCoin> GetWalletOutputs(KeyManager keyManager)
	{
		foreach (var coin in WalletOutputs)
		{
			if (keyManager.TryGetKeyForScriptPubKey(coin.ScriptPubKey, out _))
			{
				yield return coin;
			}
		}
	}

	public IEnumerable<TxIn> GetForeignInputs(KeyManager keyManager)
	{
		var walletInputs = GetWalletInputs(keyManager).ToList();

		foreach (var txIn in Transaction.Inputs)
		{
			if (walletInputs.All(x => x.TransactionId != txIn.PrevOut.Hash || x.Index != txIn.PrevOut.N))
			{
				yield return txIn;
			}
		}
	}

	public IEnumerable<IndexedTxOut> GetForeignOutputs(KeyManager keyManager)
	{
		var walletOutputs = GetWalletOutputs(keyManager).ToList();

		for (uint i = 0; i < Transaction.Outputs.Count; i++)
		{
			var txOut = Transaction.Outputs[i];

			if (walletOutputs.All(x => x.Index != i))
			{
				yield return new IndexedTxOut { N = i, TxOut = txOut, Transaction = Transaction };
			}
		}
	}

	public bool IsCpfpable(KeyManager keyManager) =>
		!keyManager.IsWatchOnly && !keyManager.IsHardwareWallet // [Difficultly] Watch-only and hardware wallets are problematic. It remains a ToDo for the future.
		&& !Confirmed // [Impossibility] We can only speed up unconfirmed transactions.
		&& GetWalletOutputs(keyManager).Any(x => !x.IsSpent()); // [Impossibility] If I have an unspent wallet output, then we can CPFP it.

	public bool IsRbfable(KeyManager keyManager) =>
		!keyManager.IsWatchOnly && !keyManager.IsHardwareWallet // [Difficultly] Watch-only and hardware wallets are problematic. It remains a ToDo for the future.
		&& !Confirmed // [Impossibility] We can only speed up unconfirmed transactions.
		&& !GetForeignInputs(keyManager).Any() // [Impossibility] Must not have foreign inputs, otherwise we couldn't do RBF.
		&& WalletOutputs.All(x => !x.IsSpent()); // [Dangerous] All the outputs we know of should not be spent, otherwise we shouldn't do RBF.

	public bool IsSpeedupable(KeyManager keyManager) =>
		IsCpfpable(keyManager) || IsRbfable(keyManager) || ChildrenPayForThisTx.Any(x => x.IsSpeedupable(keyManager)); // [Impossibility] We can only speed up if we can either CPFP or RBF or a child is speedupable.

	public bool IsCancellable(KeyManager keyManager) =>
		IsRbfable(keyManager) // [Impossibility] We can only cancel with RBF.
		&& GetForeignOutputs(keyManager).Any() // [Nonsensical] Cancellation of a transaction in which only we have outputs in, is non-sensical.
		&& !IsCancellation; // [Nonsensical] It is non-sensical to cancel a cancellation transaction.

	public bool TryAddWalletInput(SmartCoin input)
	{
		lock (_stateLock)
		{
			if (_walletInputsInternal.Add(input))
			{
				_walletInputsSnapshot = null;
				ForeignInputsCache = null;
				WalletVirtualInputsCache = null;
				return true;
			}
			return false;
		}
	}

	public bool TryAddWalletOutput(SmartCoin output)
	{
		lock (_stateLock)
		{
			if (_walletOutputsInternal.Add(output))
			{
				_walletOutputsSnapshot = null;
				ForeignOutputsCache = null;
				WalletVirtualOutputsCache = null;
				ForeignVirtualOutputsCache = null;
				return true;
			}
			return false;
		}
	}

	public bool TryRemoveWalletOutput(SmartCoin output)
	{
		lock (_stateLock)
		{
			if (_walletOutputsInternal.Remove(output))
			{
				_walletOutputsSnapshot = null;
				ForeignOutputsCache = null;
				WalletVirtualOutputsCache = null;
				ForeignVirtualOutputsCache = null;
				return true;
			}
			return false;
		}
	}

	/// <summary>Update the transaction with the data acquired from another transaction. (For example merge their labels.)</summary>
	public bool TryUpdate(SmartTransaction tx)
	{
		var updated = false;

		// If this is not the same tx, then don't update.
		if (this != tx)
		{
			throw new InvalidOperationException($"{GetHash()} != {tx.GetHash()}");
		}

		// Snapshot the other transaction first, so we never hold the locks of two instances at once
		// (a.TryUpdate(b) racing b.TryUpdate(a) would deadlock).
		var (otherHeight, otherBlockHash, otherBlockIndex) = tx.GetBlockInfo();
		var otherFirstSeen = tx.FirstSeen;
		var otherLabels = tx.Labels;
		var otherIsReplacement = tx.IsReplacement;
		var otherIsSpeedup = tx.IsSpeedup;
		var otherIsCancellation = tx.IsCancellation;

		lock (_stateLock)
		{
			// Set the height related properties.
			if (otherHeight is ChainHeight)
			{
				if (_height != otherHeight)
				{
					_height = otherHeight;
					updated = true;
				}

				if (otherBlockHash is { } && _blockHash != otherBlockHash)
				{
					_blockHash = otherBlockHash;
					_blockIndex = otherBlockIndex;
					updated = true;
				}
			}
			else if (_height == Height.Unknown && otherHeight == Height.Mempool)
			{
				_height = otherHeight;
				updated = true;
			}

			// Always the earlier seen is the firstSeen.
			if (otherFirstSeen < _firstSeen)
			{
				_firstSeen = otherFirstSeen;
				updated = true;
			}

			// Merge labels.
			if (_labels != otherLabels)
			{
				_labels = LabelsArray.Merge(_labels, otherLabels);
				updated = true;
			}

			// If we have a flag set on the other, then we make sure it is set on this as well.
			if (_isReplacement is false && otherIsReplacement is true)
			{
				_isReplacement = true;
				updated = true;
			}
			if (_isSpeedup is false && otherIsSpeedup is true)
			{
				_isSpeedup = true;
				updated = true;
			}
			if (_isCancellation is false && otherIsCancellation is true)
			{
				_isCancellation = true;
				updated = true;
			}

			// If we have witness on the other tx, then we should have it on this as well.
			for (int i = 0; i < Transaction.Inputs.Count; i++)
			{
				var input = Transaction.Inputs[i];
				var otherInput = tx.Transaction.Inputs[i];

				if ((input.WitScript is null || input.WitScript == WitScript.Empty) && (otherInput.WitScript is not null && otherInput.WitScript != WitScript.Empty))
				{
					input.WitScript = otherInput.WitScript;
					updated = true;
				}
			}
		}

		return updated;
	}

	public void SetReplacement()
	{
		lock (_stateLock)
		{
			_isReplacement = true;
		}
	}

	public void SetSpeedup()
	{
		lock (_stateLock)
		{
			_isSpeedup = true;
		}
	}

	public void SetCancellation()
	{
		lock (_stateLock)
		{
			_isCancellation = true;
		}
	}

	public void SetUnconfirmed()
	{
		lock (_stateLock)
		{
			_height = Height.Mempool;
			_blockHash = null;
			_blockIndex = 0;
		}
	}

	/// <summary>Moves the transaction to the mempool unless its height is already known (e.g. it confirmed meanwhile).</summary>
	/// <returns><c>true</c> if the height was changed.</returns>
	public bool SetMempoolIfUnknown()
	{
		lock (_stateLock)
		{
			if (_height != Height.Unknown)
			{
				return false;
			}

			_height = Height.Mempool;
			return true;
		}
	}

	public bool IsOwnCoinjoin()
	   => WalletInputs.Count != 0 // We must be a participant in order for this transaction to be our coinjoin.
	   && Transaction.Inputs.Count != WalletInputs.Count; // Some inputs must not be ours for it to be a coinjoin.

	public bool IsSegwitWithoutWitness => !Transaction.HasWitness && Transaction.Inputs.Any(x => x.ScriptSig == Script.Empty);

	/// <summary>
	/// We know the fee when we have all the inputs.
	/// </summary>
	public bool TryGetFee([NotNullWhen(true)] out Money? fee)
	{
		if (ForeignInputs.Count != 0)
		{
			fee = null;
			return false;
		}
		else
		{
			fee = Transaction.GetFee(WalletInputs.Select(x => x.Coin).ToArray());
			return true;
		}
	}

	/// <summary>
	/// We know the fee rate when we have all the inputs and the virtual size for the tx.
	/// </summary>
	public bool TryGetFeeRate([NotNullWhen(true)] out FeeRate? feeRate)
	{
		if (ForeignInputs.Count != 0 || IsSegwitWithoutWitness)
		{
			feeRate = null;
			return false;
		}
		else
		{
			feeRate = Transaction.GetFeeRate(WalletInputs.Select(x => x.Coin).ToArray());
			return true;
		}
	}

	public bool TryGetLargestCPFP(KeyManager keyManage, [NotNullWhen(true)] out SmartTransaction? largestCpfp)
	{
		largestCpfp = ChildrenPayForThisTx
			.Where(x => x.IsSpeedupable(keyManage))
			.MaxBy(x => x.Transaction.Outputs.Sum(o => o.Value));

		return largestCpfp is not null;
	}

	public override bool Equals(object? obj) => Equals(obj as SmartTransaction);

	public bool Equals(SmartTransaction? other) => this == other;

	public override int GetHashCode() => GetHash().GetHashCode();

	public static bool operator ==(SmartTransaction? x, SmartTransaction? y) => y?.GetHash() == x?.GetHash();

	public static bool operator !=(SmartTransaction? x, SmartTransaction? y) => !(x == y);
}
