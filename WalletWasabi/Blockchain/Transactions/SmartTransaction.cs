using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.Transactions;

[JsonObject(MemberSerialization.OptIn)]
public class SmartTransaction : IEquatable<SmartTransaction>
{
	#region Constructors

	private Lazy<long[]> _outputValues;
	private Lazy<bool> _isWasabi2Cj;

	public SmartTransaction(Transaction transaction, Height height, uint256? blockHash = null, int blockIndex = 0, LabelsArray? labels = null, bool isReplacement = false, DateTimeOffset firstSeen = default)
	{
		Transaction = transaction;

		// Because we don't modify those transactions, we can cache the hash
		Transaction.PrecomputeHash(false, true);

		Labels = labels ?? LabelsArray.Empty;

		Height = height;
		BlockHash = blockHash;
		BlockIndex = blockIndex;

		FirstSeen = firstSeen == default ? DateTimeOffset.UtcNow : firstSeen;

		IsReplacement = isReplacement;

		WalletInputsInternal = new HashSet<SmartCoin>(Transaction.Inputs.Count);
		WalletOutputsInternal = new HashSet<SmartCoin>(Transaction.Outputs.Count);

		_outputValues = new Lazy<long[]>(() => Transaction.Outputs.Select(x => x.Value.Satoshi).ToArray(), true);
		_isWasabi2Cj = new Lazy<bool>(
			() => Transaction.Outputs.Count >= 2 // Sanity check.
			&& Transaction.Inputs.Count >= 50 // 50 was the minimum input count at the beginning of Wasabi 2.
			&& OutputValues.Count(x => BlockchainAnalyzer.StdDenoms.Contains(x)) > OutputValues.Length * 0.8 // Most of the outputs contains the denomination.
			&& OutputValues.Zip(OutputValues.Skip(1)).All(p => p.First >= p.Second), // Outputs are ordered descending.
			isThreadSafe: true);
	}

	#endregion Constructors

	#region Members

	public long[] OutputValues => _outputValues.Value;
	public bool IsWasabi2Cj => _isWasabi2Cj.Value;

	/// <summary>Coins those are on the input side of the tx and belong to ANY loaded wallet. Later if more wallets are loaded this list can increase.</summary>
	private HashSet<SmartCoin> WalletInputsInternal { get; }

	/// <summary>Coins those are on the output side of the tx and belong to ANY loaded wallet. Later if more wallets are loaded this list can increase.</summary>
	private HashSet<SmartCoin> WalletOutputsInternal { get; }

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

	public IReadOnlyCollection<SmartCoin> WalletInputs => WalletInputsInternal;

	public IReadOnlyCollection<SmartCoin> WalletOutputs => WalletOutputsInternal;

	public IReadOnlyCollection<IndexedTxIn> ForeignInputs
	{
		get
		{
			if (ForeignInputsCache is null)
			{
				var walletInputOutpoints = WalletInputs.Select(smartCoin => smartCoin.Outpoint).ToHashSet();
				ForeignInputsCache = Transaction.Inputs.AsIndexedInputs().Where(i => !walletInputOutpoints.Contains(i.PrevOut)).ToHashSet();
			}
			return ForeignInputsCache;
		}
	}

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
			if (!walletInputs.Any(x => x.TransactionId == txIn.PrevOut.Hash && x.Index == txIn.PrevOut.N))
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

	public IReadOnlyCollection<IndexedTxOut> ForeignOutputs
	{
		get
		{
			if (ForeignOutputsCache is null)
			{
				var walletOutputIndices = WalletOutputs.Select(smartCoin => smartCoin.Outpoint.N).ToHashSet();
				ForeignOutputsCache = Transaction.Outputs.AsIndexedOutputs().Where(o => !walletOutputIndices.Contains(o.N)).ToHashSet();
			}
			return ForeignOutputsCache;
		}
	}

	/// <summary>Wallet inputs with the same script are virtually considered to be the same by blockchain analysis.</summary>
	public IReadOnlyCollection<WalletVirtualInput> WalletVirtualInputs
	{
		get
		{
			WalletVirtualInputsCache ??= WalletInputs
					.GroupBy(i => i.HdPubKey.PubKeyHash.ToBytes(), new ByteArrayEqualityComparer())
					.Select(g => new WalletVirtualInput(g.Key, g.ToHashSet()))
					.ToHashSet();
			return WalletVirtualInputsCache;
		}
	}

	/// <summary>Wallet outputs with the same script are virtually considered to be the same by blockchain analysis.</summary>
	public IReadOnlyCollection<WalletVirtualOutput> WalletVirtualOutputs
	{
		get
		{
			WalletVirtualOutputsCache ??= WalletOutputs
					.GroupBy(o => o.HdPubKey.PubKeyHash.ToBytes(), new ByteArrayEqualityComparer())
					.Select(g => new WalletVirtualOutput(g.Key, g.ToHashSet()))
					.ToHashSet();
			return WalletVirtualOutputsCache;
		}
	}

	/// <summary>Foreign outputs with the same script are virtually considered to be the same by blockchain analysis.</summary>
	public IReadOnlyCollection<ForeignVirtualOutput> ForeignVirtualOutputs
	{
		get
		{
			ForeignVirtualOutputsCache ??= ForeignOutputs
					.GroupBy(o => o.TxOut.ScriptPubKey.ExtractKeyId(), new ByteArrayEqualityComparer())
					.Select(g => new ForeignVirtualOutput(g.Key, g.Sum(o => o.TxOut.Value), g.Select(o => new OutPoint(GetHash(), o.N)).ToHashSet()))
					.ToHashSet();
			return ForeignVirtualOutputsCache;
		}
	}

	[JsonProperty]
	[JsonConverter(typeof(TransactionJsonConverter))]
	public Transaction Transaction { get; }

	[JsonProperty]
	[JsonConverter(typeof(HeightJsonConverter))]
	public Height Height { get; private set; }

	[JsonProperty]
	[JsonConverter(typeof(Uint256JsonConverter))]
	public uint256? BlockHash { get; private set; }

	[JsonProperty]
	public int BlockIndex { get; private set; }

	[JsonProperty(PropertyName = "Label")]
	[JsonConverter(typeof(LabelsArrayJsonConverter))]
	public LabelsArray Labels { get; set; }

	[JsonProperty]
	[JsonConverter(typeof(DateTimeOffsetUnixSecondsConverter))]
	public DateTimeOffset FirstSeen { get; private set; }

	public bool IsSpeedupable => !Confirmed;

	public bool IsCancelable(KeyManager keyManager) => !Confirmed && !GetForeignInputs(keyManager).Any() && GetForeignOutputs(keyManager).Any() && IsRBF;

	[JsonProperty(PropertyName = "FirstSeenIfMempoolTime")]
	[JsonConverter(typeof(BlockCypherDateTimeOffsetJsonConverter))]
	[Obsolete("This property exists only for json backwards compatibility. If someone tries to set it, it'll set the FirstSeen. https://stackoverflow.com/a/43715009/2061103", error: true)]
	[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "json backwards compatibility")]
	private DateTimeOffset? FirstSeenCompatibility
	{
		set
		{
			// If it's null, let the default of FirstSeen to be set.
			// If it's not null, then check if FirstSeen has just been recently set to UtcNow which is its default.
			if (value.HasValue && DateTimeOffset.UtcNow - FirstSeen < TimeSpan.FromSeconds(1))
			{
				FirstSeen = value.Value;
			}
		}
	}

	[JsonProperty]
	public bool IsReplacement { get; private set; }

	public bool Confirmed => Height.Type == HeightType.Chain;

	public uint256 GetHash() => Transaction.GetHash();

	public int GetConfirmationCount(Height bestHeight) => Height == Height.Mempool ? 0 : bestHeight.Value - Height.Value + 1;

	/// <summary>
	/// A transaction can signal that is replaceable by fee in two ways:
	/// * Explicitly by using a nSequence &lt; (0xffffffff - 1) or,
	/// * Implicitly in case one of its unconfirmed ancestors are replaceable
	/// </summary>
	public bool IsRBF => !Confirmed && (Transaction.RBF || IsReplacement || WalletInputs.Any(x => x.IsReplaceable()));

	#endregion Members

	public bool TryAddWalletInput(SmartCoin input)
	{
		if (WalletInputsInternal.Add(input))
		{
			ForeignInputsCache = null;
			WalletVirtualInputsCache = null;
			return true;
		}
		return false;
	}

	public bool TryAddWalletOutput(SmartCoin output)
	{
		if (WalletOutputsInternal.Add(output))
		{
			ForeignOutputsCache = null;
			WalletVirtualOutputsCache = null;
			ForeignVirtualOutputsCache = null;
			return true;
		}
		return false;
	}

	public bool TryRemoveWalletInput(SmartCoin input)
	{
		if (WalletInputsInternal.Remove(input))
		{
			ForeignInputsCache = null;
			WalletVirtualInputsCache = null;
			return true;
		}
		return false;
	}

	public bool TryRemoveWalletOutput(SmartCoin output)
	{
		if (WalletOutputsInternal.Remove(output))
		{
			ForeignOutputsCache = null;
			WalletVirtualOutputsCache = null;
			ForeignVirtualOutputsCache = null;
			return true;
		}
		return false;
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

		// Set the height related properties, only if confirmed.
		if (tx.Confirmed)
		{
			if (Height != tx.Height)
			{
				Height = tx.Height;
				updated = true;
			}

			if (tx.BlockHash is { } && BlockHash != tx.BlockHash)
			{
				BlockHash = tx.BlockHash;
				BlockIndex = tx.BlockIndex;
				updated = true;
			}
		}

		// Always the earlier seen is the firstSeen.
		if (tx.FirstSeen < FirstSeen)
		{
			FirstSeen = tx.FirstSeen;
			updated = true;
		}

		// Merge labels.
		if (Labels != tx.Labels)
		{
			Labels = LabelsArray.Merge(Labels, tx.Labels);
			updated = true;
		}

		return updated;
	}

	public void SetReplacement()
	{
		IsReplacement = true;
	}

	/// <summary>First looks at height, then block index, then mempool FirstSeen.</summary>
	public static IComparer<SmartTransaction> GetBlockchainComparer()
	{
		return Comparer<SmartTransaction>.Create((a, b) =>
		{
			var heightCompareResult = a.Height.CompareTo(b.Height);
			if (heightCompareResult != 0)
			{
				return heightCompareResult;
			}

			// If mempool this should be 0, so they should be equal so no worry about it.
			var blockIndexCompareResult = a.BlockIndex.CompareTo(b.BlockIndex);
			if (blockIndexCompareResult != 0)
			{
				return blockIndexCompareResult;
			}

			var firstSeenCompareResult = a.FirstSeen.CompareTo(b.FirstSeen);
			return firstSeenCompareResult;
		});
	}

	public void SetUnconfirmed()
	{
		Height = Height.Mempool;
		BlockHash = null;
		BlockIndex = 0;
	}

	public bool IsOwnCoinjoin()
	   => WalletInputs.Any() // We must be a participant in order for this transaction to be our coinjoin.
	   && Transaction.Inputs.Count != WalletInputs.Count; // Some inputs must not be ours for it to be a coinjoin.

	#region LineSerialization

	public string ToLine()
	{
		// GetHash is also serialized, so file can be interpreted with our eyes better.

		return string.Join(
			':',
			GetHash(),
			Transaction.ToHex(),
			Height,
			BlockHash,
			BlockIndex,
			Labels,
			FirstSeen.ToUnixTimeSeconds(),
			IsReplacement);
	}

	public static SmartTransaction FromLine(string line, Network expectedNetwork)
	{
		var parts = line.Split(':', StringSplitOptions.None).Select(x => x.Trim()).ToArray();

		var transactionString = parts[1];
		Transaction transaction = Transaction.Parse(transactionString, expectedNetwork);

		try
		{
			// First is redundant txHash serialization.
			var heightString = parts[2];
			var blockHashString = parts[3];
			var blockIndexString = parts[4];
			var labelString = parts[5];
			var firstSeenString = parts[6];
			var isReplacementString = parts[7];

			if (!Height.TryParse(heightString, out Height height))
			{
				height = Height.Unknown;
			}
			if (!uint256.TryParse(blockHashString, out var blockHash))
			{
				blockHash = null;
			}
			if (!int.TryParse(blockIndexString, out int blockIndex))
			{
				blockIndex = 0;
			}
			var label = new LabelsArray(labelString);
			DateTimeOffset firstSeen = default;
			if (long.TryParse(firstSeenString, out long unixSeconds))
			{
				firstSeen = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
			}
			if (!bool.TryParse(isReplacementString, out bool isReplacement))
			{
				isReplacement = false;
			}

			return new SmartTransaction(transaction, height, blockHash, blockIndex, label, isReplacement, firstSeen);
		}
		catch (Exception ex)
		{
			Logger.LogDebug(ex);
			return new SmartTransaction(transaction, Height.Unknown);
		}
	}

	#endregion LineSerialization

	#region EqualityAndComparison

	public override bool Equals(object? obj) => Equals(obj as SmartTransaction);

	public bool Equals(SmartTransaction? other) => this == other;

	public override int GetHashCode() => GetHash().GetHashCode();

	public static bool operator ==(SmartTransaction? x, SmartTransaction? y) => y?.GetHash() == x?.GetHash();

	public static bool operator !=(SmartTransaction? x, SmartTransaction? y) => !(x == y);

	#endregion EqualityAndComparison
}
