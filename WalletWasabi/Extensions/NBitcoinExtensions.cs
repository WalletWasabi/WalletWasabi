using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Extensions;

public static class NBitcoinExtensions
{
	public static async Task<Block> DownloadBlockAsync(this Node node, uint256 hash, CancellationToken cancellationToken)
	{
		if (node.State == NodeState.Connected)
		{
			node.VersionHandshake(cancellationToken);
		}

		using var listener = node.CreateListener();
		var getData = new GetDataPayload(new InventoryVector(InventoryType.MSG_BLOCK, hash));
		await node.SendMessageAsync(getData).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();

		// Bitcoin Core processes the messages sequentially and does not send a NOTFOUND message if the remote node is pruned and the data not available.
		// A good way to get any feedback about whether the node knows the block or not is to send a ping request.
		// If block is not known by the remote node, the pong will be sent immediately, else it will be sent after the block download.
		ulong pingNonce = RandomUtils.GetUInt64();
		await node.SendMessageAsync(new PingPayload() { Nonce = pingNonce }).ConfigureAwait(false);
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var message = listener.ReceiveMessage(cancellationToken);
			if (message.Message.Payload is NotFoundPayload ||
				(message.Message.Payload is PongPayload p && p.Nonce == pingNonce))
			{
				throw new InvalidOperationException($"Disconnected node, because it does not have the block data.");
			}
			else if (message.Message.Payload is BlockPayload b && b.Object?.GetHash() == hash)
			{
				return b.Object;
			}
		}
	}

	public static string ToHex(this IBitcoinSerializable me)
	{
		return ByteHelpers.ToHex(me.ToBytes());
	}

	public static void FromHex(this IBitcoinSerializable me, string hex)
	{
		Guard.NotNullOrEmptyOrWhitespace(nameof(hex), hex);
		me.FromBytes(ByteHelpers.FromHex(hex));
	}

	/// <summary>
	/// Based on transaction data, it decides if it's possible that native segwit script played a par in this transaction.
	/// </summary>
	public static bool SegWitInvolved(this Transaction me) =>
		me.Inputs.Any(i => Script.IsNullOrEmpty(i.ScriptSig)) ||
		me.Outputs.Any(o => o.ScriptPubKey.IsScriptType(ScriptType.Witness));

	public static IEnumerable<(Money value, int count)> GetIndistinguishableOutputs(this Transaction me, bool includeSingle)
	{
		return me.Outputs.GroupBy(x => x.Value)
			.ToDictionary(x => x.Key, y => y.Count())
			.Select(x => (x.Key, x.Value))
			.Where(x => includeSingle || x.Value > 1);
	}

	/// <summary>
	/// Careful, if it's in a legacy block then this won't work.
	/// </summary>
	public static bool HasWitScript(this TxIn me)
	{
		Guard.NotNull(nameof(me), me);

		bool notNull = me.WitScript is not null;
		bool notEmpty = me.WitScript != WitScript.Empty;
		return notNull && notEmpty;
	}

	public static Money Percentage(this Money me, decimal perc)
	{
		return Money.Satoshis((me.Satoshi / 100m) * perc);
	}

	public static string ToZPrv(this ExtKey extKey, Network network)
	{
		var data = extKey.ToBytes();
		var version = (network == Network.Main)
			? new byte[] { (0x04), (0xB2), (0x43), (0x0C) }
			: new byte[] { (0x04), (0x5F), (0x18), (0xBC) };

		return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
	}

	public static SmartTransaction ExtractSmartTransaction(this PSBT psbt)
	{
		var extractedTx = psbt.ExtractTransaction();
		return new SmartTransaction(extractedTx, Height.Unknown);
	}

	public static SmartTransaction ExtractSmartTransaction(this PSBT psbt, SmartTransaction unsignedSmartTransaction)
	{
		var extractedTx = psbt.ExtractTransaction();
		return new SmartTransaction(
			extractedTx,
			unsignedSmartTransaction.Height,
			unsignedSmartTransaction.BlockHash,
			unsignedSmartTransaction.BlockIndex,
			unsignedSmartTransaction.Labels,
			unsignedSmartTransaction.IsReplacement,
			unsignedSmartTransaction.IsSpeedup,
			unsignedSmartTransaction.IsCancellation,
			unsignedSmartTransaction.FirstSeen);
	}

	public static void SortByAmount(this TxOutList list)
	{
		list.Sort((x, y) => x.Value.CompareTo(y.Value));
	}

	/// <param name="startWithM">The keypath will start with m/ or not.</param>
	/// <param name="format">Either h or ', eg.: m/84h/0h/0 or m/84'/0'/0</param>
	public static string ToString(this KeyPath me, bool startWithM, string format)
	{
		var toStringBuilder = new StringBuilder(me.ToString());

		if (startWithM)
		{
			toStringBuilder.Insert(0, "m/");
		}

		if (format == "h")
		{
			toStringBuilder.Replace('\'', 'h');
		}

		return toStringBuilder.ToString();
	}

	public static BitcoinWitPubKeyAddress TransformToNetwork(this BitcoinWitPubKeyAddress me, Network desiredNetwork)
	{
		Network originalNetwork = me.Network;

		if (originalNetwork == desiredNetwork)
		{
			return me;
		}

		var newAddress = new BitcoinWitPubKeyAddress(me.Hash, desiredNetwork);

		return newAddress;
	}

	public static void SortByAmount(this TxInList list, IEnumerable<Coin> coins)
	{
		var map = new Dictionary<TxIn, Coin>();
		foreach (var coin in coins)
		{
			map.Add(list.Single(x => x.PrevOut == coin.Outpoint), coin);
		}
		list.Sort((x, y) => map[x].Amount.CompareTo(map[y].Amount));
	}

	public static IEnumerable<TransactionDependencyNode> ToDependencyGraph(this IEnumerable<Transaction> txs)
	{
		var lookup = new Dictionary<uint256, TransactionDependencyNode>();
		foreach (var tx in txs)
		{
			lookup.Add(tx.GetHash(), new TransactionDependencyNode { Transaction = tx });
		}

		foreach (var node in lookup.Values)
		{
			foreach (var input in node.Transaction.Inputs)
			{
				if (lookup.TryGetValue(input.PrevOut.Hash, out var parent))
				{
					if (!node.Parents.Contains(parent))
					{
						node.Parents.Add(parent);
					}
					if (!parent.Children.Contains(node))
					{
						parent.Children.Add(node);
					}
				}
			}
		}
		var nodes = lookup.Values;
		return nodes.Where(x => x.Parents.Count == 0);
	}

	public static IEnumerable<Transaction> OrderByDependency(this IEnumerable<TransactionDependencyNode> roots)
	{
		var parentCounter = new Dictionary<TransactionDependencyNode, int>();

		void Walk(TransactionDependencyNode node)
		{
			if (!parentCounter.ContainsKey(node))
			{
				parentCounter.Add(node, node.Parents.Count);
				foreach (var child in node.Children)
				{
					Walk(child);
				}
			}
		}

		foreach (var root in roots)
		{
			Walk(root);
		}

		var nodes = parentCounter.Where(x => x.Value == 0).Select(x => x.Key).Distinct().ToArray();
		while (nodes.Any())
		{
			foreach (var node in nodes)
			{
				yield return node.Transaction;
				parentCounter.Remove(node);
				foreach (var child in node.Children)
				{
					parentCounter[child] = parentCounter[child] - 1;
				}
			}
			nodes = parentCounter.Where(x => x.Value == 0).Select(x => x.Key).Distinct().ToArray();
		}
	}

	public static ScriptPubKeyType? GetInputScriptPubKeyType(this PSBTInput i)
	{
		if (i.WitnessUtxo is null)
		{
			throw new ArgumentNullException($"{nameof(i.WitnessUtxo)} was null, can't get it's ScriptPubKey type.");
		}

		if (i.WitnessUtxo.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
		{
			return ScriptPubKeyType.Segwit;
		}

		if (i.WitnessUtxo.ScriptPubKey.IsScriptType(ScriptType.P2SH) &&
			i.FinalScriptWitness is { } witness &&
			witness.ToScript().IsScriptType(ScriptType.P2WPKH))
		{
			return ScriptPubKeyType.SegwitP2SH;
		}

		return null;
	}

	/// <summary>
	/// Tries to equip the PSBT with input and output keypaths on best effort.
	/// </summary>
	public static void AddKeyPaths(this PSBT psbt, KeyManager keyManager)
	{
		if (keyManager.MasterFingerprint.HasValue)
		{
			var fp = keyManager.MasterFingerprint.Value;

			// Add input keypaths.
			foreach (var script in psbt.Inputs.Select(x => x.WitnessUtxo?.ScriptPubKey).ToArray())
			{
				if (script is { })
				{
					if (keyManager.TryGetKeyForScriptPubKey(script, out HdPubKey? hdPubKey))
					{
						psbt.AddKeyPath(fp, hdPubKey, script);
					}
				}
			}

			// Add output keypaths.
			foreach (var script in psbt.Outputs.Select(x => x.ScriptPubKey).ToArray())
			{
				if (keyManager.TryGetKeyForScriptPubKey(script, out HdPubKey? hdPubKey))
				{
					psbt.AddKeyPath(fp, hdPubKey, script);
				}
			}
		}
	}

	public static void AddKeyPath(this PSBT psbt, HDFingerprint fp, HdPubKey hdPubKey, Script script)
	{
		var rootKeyPath = new RootedKeyPath(fp, hdPubKey.FullKeyPath);
		psbt.AddKeyPath(hdPubKey.PubKey, rootKeyPath, script);
	}

	/// <summary>
	/// Tries to equip the PSBT with previous transactions with best effort. Always <see cref="AddKeyPaths"/> first otherwise the prev tx won't be added.
	/// </summary>
	public static void AddPrevTxs(this PSBT psbt, ITransactionStore transactionStore)
	{
		// Fill out previous transactions.
		foreach (var psbtInput in psbt.Inputs)
		{
			if (transactionStore.TryGetTransaction(psbtInput.PrevOut.Hash, out var tx))
			{
				psbtInput.NonWitnessUtxo = tx.Transaction;
			}
			else
			{
				Logger.LogDebug($"Transaction id: {psbtInput.PrevOut.Hash} is missing from the {nameof(transactionStore)}. Ignoring...");
			}
		}
	}

	public static FeeRate GetSanityFeeRate(this MemPoolInfo me)
	{
		var mempoolMinFee = (decimal)me.MemPoolMinFee;

		// Make sure to be prepared for mempool spikes.
		var spikeSanity = mempoolMinFee * 1.5m;

		var sanityFee = FeeRate.Max(new FeeRate(Money.Coins(spikeSanity)), new FeeRate(2m));
		return sanityFee;
	}

	public static int EstimateOutputVsize(this Script scriptPubKey) =>
		new TxOut(Money.Zero, scriptPubKey).GetSerializedSize();

	public static int EstimateInputVsize(this Script scriptPubKey) =>
		scriptPubKey.GetScriptType().EstimateInputVsize();

	public static int EstimateInputVsize(this ScriptType scriptType) =>
		scriptType switch
		{
			ScriptType.P2WPKH => Constants.P2wpkhInputVirtualSize,
			ScriptType.Taproot => Constants.P2trInputVirtualSize,
			ScriptType.P2PKH => Constants.P2pkhInputVirtualSize,
			ScriptType.P2SH => Constants.P2shInputVirtualSize,
			ScriptType.P2WSH => Constants.P2wshInputVirtualSize,
			_ => throw new NotImplementedException($"Size estimation isn't implemented for provided script type.")
		};

	public static int EstimateOutputVsize(this ScriptType scriptType) =>
		scriptType switch
		{
			ScriptType.P2WPKH => Constants.P2wpkhOutputVirtualSize,
			ScriptType.Taproot => Constants.P2trOutputVirtualSize,
			ScriptType.P2PKH => Constants.P2pkhOutputVirtualSize,
			ScriptType.P2SH => Constants.P2shOutputVirtualSize,
			ScriptType.P2WSH => Constants.P2wshOutputVirtualSize,
			_ => throw new NotImplementedException($"Size estimation isn't implemented for provided script type.")
		};

	public static Money EffectiveCost(this TxOut output, FeeRate feeRate) =>
		output.Value + feeRate.GetFee(output.ScriptPubKey.EstimateOutputVsize());

	public static Money EffectiveValue(this ICoin coin, FeeRate feeRate, CoordinationFeeRate coordinationFeeRate)
		=> EffectiveValue(coin.TxOut.Value, virtualSize: coin.TxOut.ScriptPubKey.EstimateInputVsize(), feeRate, coordinationFeeRate);

	public static Money EffectiveValue(this ISmartCoin coin, FeeRate feeRate, CoordinationFeeRate coordinationFeeRate)
		=> EffectiveValue(coin.Amount, virtualSize: coin.ScriptType.EstimateInputVsize(), feeRate, coordinationFeeRate);

	private static Money EffectiveValue(Money amount, int virtualSize, FeeRate feeRate, CoordinationFeeRate coordinationFeeRate)
	{
		var networkFee = feeRate.GetFee(virtualSize);
		var coordinationFee = coordinationFeeRate.GetFee(amount);

		return amount - networkFee - coordinationFee;
	}

	public static Money EffectiveValue(this SmartCoin coin, FeeRate feeRate, CoordinationFeeRate coordinationFeeRate) =>
		EffectiveValue(coin.Coin, feeRate, coordinationFeeRate);

	public static Money EffectiveValue(this SmartCoin coin, FeeRate feeRate) =>
		EffectiveValue(coin.Coin, feeRate, CoordinationFeeRate.Zero);

	public static Money EffectiveValue(this ISmartCoin coin, FeeRate feeRate) =>
		EffectiveValue(coin, feeRate, CoordinationFeeRate.Zero);

	public static T FromBytes<T>(byte[] input) where T : IBitcoinSerializable, new()
	{
		BitcoinStream inputStream = new(input);
		var instance = new T();
		inputStream.ReadWrite(instance);
		if (inputStream.Inner.Length != inputStream.Inner.Position)
		{
			throw new FormatException("Expected end of stream");
		}

		return instance;
	}

	/// <summary>
	/// Extracts a unique public key identifier. If it can't do that, then it returns the scriptPubKey byte array.
	/// </summary>
	public static byte[] ExtractKeyId(this Script scriptPubKey)
	{
		return scriptPubKey.TryGetScriptType() switch
		{
			ScriptType.P2WPKH => PayToWitPubKeyHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey)!.ToBytes(),
			ScriptType.P2PKH => PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey)!.ToBytes(),
			ScriptType.P2PK => PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey)!.ToBytes(),
			_ => scriptPubKey.ToBytes()
		};
	}

	public static ScriptType GetScriptType(this Script script)
	{
		return TryGetScriptType(script) ?? throw new NotImplementedException($"Unsupported script type.");
	}

	public static ScriptType? TryGetScriptType(this Script script)
	{
		foreach (ScriptType scriptType in new ScriptType[] { ScriptType.P2WPKH, ScriptType.P2PKH, ScriptType.P2PK, ScriptType.Taproot })
		{
			if (script.IsScriptType(scriptType))
			{
				return scriptType;
			}
		}

		return null;
	}

	public static BitcoinSecret GetBitcoinSecret(this ExtKey hdKey, Network network, Script scriptPubKey)
		=> GetBitcoinSecret(network, hdKey.PrivateKey, scriptPubKey);

	public static BitcoinSecret GetBitcoinSecret(Network network, Key privateKey, Script scriptPubKey)
	{
		var derivedScriptPubKeyType = scriptPubKey switch
		{
			_ when scriptPubKey.IsScriptType(ScriptType.P2WPKH) => ScriptPubKeyType.Segwit,
			_ when scriptPubKey.IsScriptType(ScriptType.Taproot) => ScriptPubKeyType.TaprootBIP86,
			_ => throw new NotSupportedException("Not supported script type.")
		};

		if (privateKey.PubKey.GetScriptPubKey(derivedScriptPubKeyType) != scriptPubKey)
		{
			throw new InvalidOperationException("The key cannot generate the utxo scriptPubKey. This could happen if the wallet password is not the correct one.");
		}

		return privateKey.GetBitcoinSecret(network);
	}

	public static OwnershipProof GetOwnershipProof(Key masterKey, BitcoinSecret secret, Script scriptPubKey, CoinJoinInputCommitmentData commitmentData)
	{
		var identificationMasterKey = Slip21Node.FromSeed(masterKey.ToBytes());
		var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019")
			.DeriveChild("Ownership identification key").Key;

		var signingKey = secret.PrivateKey;
		var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
			signingKey,
			new OwnershipIdentifier(identificationKey, scriptPubKey),
			commitmentData,
			scriptPubKey.IsScriptType(ScriptType.P2WPKH)
				? ScriptPubKeyType.Segwit
				: ScriptPubKeyType.TaprootBIP86);

		return ownershipProof;
	}

	public static Money GetFeeWithZero(this FeeRate feeRate, int virtualSize) =>
		feeRate == FeeRate.Zero ? Money.Zero : feeRate.GetFee(virtualSize);

	/// <remarks>NBitcoin does not provide a try-parse method.</remarks>
	public static bool TryParseBitcoinAddressForNetwork(string address, Network network, [NotNullWhen(true)] out BitcoinAddress? bitcoinAddress)
	{
		try
		{
			bitcoinAddress = Network.Parse<BitcoinAddress>(address, network);
			return true;
		}
		catch
		{
			bitcoinAddress = null;
			return false;
		}
	}
}
