using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Models;

namespace NBitcoin;

public static class NBitcoinExtensions
{
	private static NumberFormatInfo CurrencyNumberFormat = new()
	{
		NumberGroupSeparator = " ",
		NumberDecimalDigits = 0
	};

	public static async Task<Block> DownloadBlockAsync(this Node node, uint256 hash, CancellationToken cancellationToken)
	{
		if (node.State == NodeState.Connected)
		{
			node.VersionHandshake(cancellationToken);
		}

		using var listener = node.CreateListener();
		var getdata = new GetDataPayload(new InventoryVector(node.AddSupportedOptions(InventoryType.MSG_BLOCK), hash));
		await node.SendMessageAsync(getdata).ConfigureAwait(false);
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
				throw new InvalidOperationException($"Disconnected local node, because it does not have the block data.");
			}
			else if (message.Message.Payload is BlockPayload b && b.Object?.GetHash() == hash)
			{
				return b.Object;
			}
		}
	}

	public static IEnumerable<Coin> GetCoins(this TxOutList me, Script script)
	{
		return me.AsCoins().Where(c => c.ScriptPubKey == script);
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
	public static bool PossiblyP2WPKHInvolved(this Transaction me)
	{
		// We omit Guard, because it's performance critical in Wasabi.
		// We start with the inputs, because, this check is faster.
		// Note: by testing performance the order does not seem to affect the speed of loading the wallet.
		foreach (TxIn input in me.Inputs)
		{
			if (input.ScriptSig is null || input.ScriptSig == Script.Empty)
			{
				return true;
			}
		}
		foreach (TxOut output in me.Outputs)
		{
			if (output.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasIndistinguishableOutputs(this Transaction me)
	{
		var hashset = new HashSet<long>();
		foreach (var name in me.Outputs.Select(x => x.Value))
		{
			if (!hashset.Add(name))
			{
				return true;
			}
		}
		return false;
	}

	public static IEnumerable<(Money value, int count)> GetIndistinguishableOutputs(this Transaction me, bool includeSingle)
	{
		return me.Outputs.GroupBy(x => x.Value)
			.ToDictionary(x => x.Key, y => y.Count())
			.Select(x => (x.Key, x.Value))
			.Where(x => includeSingle || x.Value > 1);
	}

	public static int GetAnonymitySet(this Transaction me, uint outputIndex)
		=> me.GetAnonymitySets(new[] { outputIndex }).First().Value;

	public static IDictionary<uint, int> GetAnonymitySets(this Transaction me, IEnumerable<uint> outputIndices)
	{
		var anonsets = new Dictionary<uint, int>();
		var inputCount = me.Inputs.Count;

		var indistinguishableOutputs = me.Outputs
			.GroupBy(x => x.ScriptPubKey)
			.ToDictionary(x => x.Key, y => y.Sum(z => z.Value))
			.GroupBy(x => x.Value)
			.ToDictionary(x => x.Key, y => y.Count());

		foreach (var outputIndex in outputIndices)
		{
			// 1. Get the output corresponting to the output index.
			var output = me.Outputs[outputIndex];

			// 2. Get the number of equal outputs.
			int equalOutputs = indistinguishableOutputs[output.Value];

			// 3. Anonymity set cannot be larger than the number of inputs.
			var anonSet = Math.Min(equalOutputs, inputCount);

			anonsets.Add(outputIndex, anonSet);
		}

		return anonsets;
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

	public static decimal ToUsd(this Money me, decimal btcExchangeRate)
	{
		return me.ToDecimal(MoneyUnit.BTC) * btcExchangeRate;
	}

	public static bool VerifyMessage(this BitcoinWitPubKeyAddress address, uint256 messageHash, CompactSignature signature)
	{
		PubKey pubKey = PubKey.RecoverCompact(messageHash, signature);
		return pubKey.WitHash == address.Hash;
	}

	/// <summary>
	/// If scriptpubkey is already present, just add the value.
	/// </summary>
	public static void AddWithOptimize(this TxOutList me, Money money, Script scriptPubKey)
	{
		var found = me.FirstOrDefault(x => x.ScriptPubKey == scriptPubKey);
		if (found is { })
		{
			found.Value += money;
		}
		else
		{
			me.Add(money, scriptPubKey);
		}
	}

	public static string ToZpub(this ExtPubKey extPubKey, Network network)
	{
		var data = extPubKey.ToBytes();
		var version = (network == Network.Main)
			? new byte[] { (0x04), (0xB2), (0x47), (0x46) }
			: new byte[] { (0x04), (0x5F), (0x1C), (0xF6) };

		return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
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
		return new SmartTransaction(extractedTx,
			unsignedSmartTransaction.Height,
			unsignedSmartTransaction.BlockHash,
			unsignedSmartTransaction.BlockIndex,
			unsignedSmartTransaction.Label,
			unsignedSmartTransaction.IsReplacement,
			unsignedSmartTransaction.FirstSeen);
	}

	public static void SortByAmount(this TxOutList list)
	{
		list.Sort((x, y) => x.Value.CompareTo(y.Value));
	}

	/// <param name="startWithM">The keypath will start with m/ or not.</param>
	/// <param name="format">h or ', eg.: m/84h/0h/0 or m/84'/0'/0</param>
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

	public static Money GetTotalFee(this FeeRate me, int vsize)
	{
		return Money.Satoshis(Math.Round(me.SatoshiPerByte * vsize));
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
		return nodes.Where(x => !x.Parents.Any());
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

	private static string ToCurrency(this Money btc, string currency, decimal exchangeRate, bool privacyMode = false)
	{
		var dollars = exchangeRate * btc.ToDecimal(MoneyUnit.BTC);

		return privacyMode
			? $"### {currency}"
			: exchangeRate == default
				? $"??? {currency}"
				: $"{dollars.ToString("N", CurrencyNumberFormat)} {currency}";
	}

	public static string ToUsdString(this Money btc, decimal usdExchangeRate, bool privacyMode = false)
	{
		return ToCurrency(btc, "USD", usdExchangeRate, privacyMode);
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
	public static void AddPrevTxs(this PSBT psbt, AllTransactionStore transactionStore)
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
				Logger.LogInfo($"Transaction id: {psbtInput.PrevOut.Hash} is missing from the {nameof(transactionStore)}. Ignoring...");
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
		scriptPubKey.IsScriptType(ScriptType.P2WPKH) switch
		{
			true => Constants.P2wpkhInputVirtualSize,
			false => throw new NotImplementedException($"Size estimation isn't implemented for provided script type.")
		};

	public static Money EffectiveCost(this TxOut output, FeeRate feeRate) =>
		output.Value + feeRate.GetFee(output.ScriptPubKey.EstimateOutputVsize());

	public static Money EffectiveValue(this Coin coin, FeeRate feeRate, CoordinationFeeRate coordinationFeeRate)
	{
		var netFee = feeRate.GetFee(coin.ScriptPubKey.EstimateInputVsize());
		var coordFee = coordinationFeeRate.GetFee(coin.Amount);

		return coin.Amount - netFee - coordFee;
	}

	public static Money EffectiveValue(this SmartCoin coin, FeeRate feeRate, CoordinationFeeRate coordinationFeeRate) =>
		EffectiveValue(coin.Coin, feeRate, coordinationFeeRate);

	public static Money EffectiveValue(this SmartCoin coin, FeeRate feeRate) =>
		EffectiveValue(coin.Coin, feeRate, CoordinationFeeRate.Zero);

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
}
