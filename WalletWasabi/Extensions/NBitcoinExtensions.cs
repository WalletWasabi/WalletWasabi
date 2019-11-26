using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Common.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace NBitcoin
{
	public static class NBitcoinExtensions
	{
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

		public static TxoRef ToTxoRef(this OutPoint me) => new TxoRef(me);

		public static IEnumerable<TxoRef> ToTxoRefs(this TxInList me)
		{
			foreach (var input in me)
			{
				yield return input.PrevOut.ToTxoRef();
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

		public static IEnumerable<(Money value, int count)> GetIndistinguishableOutputs(this Transaction me, bool includeSingle)
		{
			return me.Outputs.GroupBy(x => x.Value)
				.ToDictionary(x => x.Key, y => y.Count())
				.Select(x => (x.Key, x.Value))
				.Where(x => includeSingle || x.Value > 1);
		}

		public static int GetAnonymitySet(this Transaction me, int outputIndex)
		{
			// 1. Get the output corresponting to the output index.
			var output = me.Outputs[outputIndex];
			// 2. Get the number of equal outputs.
			int equalOutputs = me.GetIndistinguishableOutputs(includeSingle: true).Single(x => x.value == output.Value).count;
			// 3. Anonymity set cannot be larger than the number of inputs.
			var inputCount = me.Inputs.Count;
			var anonSet = Math.Min(equalOutputs, inputCount);
			return anonSet;
		}

		public static int GetAnonymitySet(this Transaction me, uint outputIndex) => GetAnonymitySet(me, (int)outputIndex);

		/// <summary>
		/// Careful, if it's in a legacy block then this won't work.
		/// </summary>
		public static bool HasWitScript(this TxIn me)
		{
			Guard.NotNull(nameof(me), me);

			bool notNull = !(me.WitScript is null);
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

		public static bool VerifyMessage(this BitcoinWitPubKeyAddress address, uint256 messageHash, byte[] signature)
		{
			PubKey pubKey = PubKey.RecoverCompact(messageHash, signature);
			return pubKey.WitHash == address.Hash;
		}

		public static bool VerifyUnblindedSignature(this Signer signer, UnblindedSignature signature, byte[] data)
		{
			uint256 hash = new uint256(Hashes.SHA256(data));
			return VerifySignature(hash, signature, signer.Key.PubKey);
		}

		public static bool VerifyUnblindedSignature(this Signer signer, UnblindedSignature signature, uint256 dataHash)
		{
			return VerifySignature(dataHash, signature, signer.Key.PubKey);
		}

		public static uint256 BlindScript(this Requester requester, PubKey signerPubKey, PubKey rPubKey, Script script)
		{
			var msg = new uint256(Hashes.SHA256(script.ToBytes()));
			return requester.BlindMessage(msg, rPubKey, signerPubKey);
		}

		public static Signer CreateSigner(this SchnorrKey schnorrKey)
		{
			var k = Guard.NotNull(nameof(schnorrKey.SignerKey), schnorrKey.SignerKey);
			var r = Guard.NotNull(nameof(schnorrKey.Rkey), schnorrKey.Rkey);
			return new Signer(k, r);
		}

		/// <summary>
		/// If scriptpubkey is already present, just add the value.
		/// </summary>
		public static void AddWithOptimize(this TxOutList me, Money money, Script scriptPubKey)
		{
			TxOut found = me.FirstOrDefault(x => x.ScriptPubKey == scriptPubKey);
			if (found != null)
			{
				found.Value += money;
			}
			else
			{
				me.Add(money, scriptPubKey);
			}
		}

		/// <summary>
		/// If scriptpubkey is already present, just add the value.
		/// </summary>
		public static void AddWithOptimize(this TxOutList me, Money money, IDestination destination)
		{
			me.AddWithOptimize(money, destination.ScriptPubKey);
		}

		/// <summary>
		/// If scriptpubkey is already present, just add the value.
		/// </summary>
		public static void AddWithOptimize(this TxOutList me, TxOut txOut)
		{
			me.AddWithOptimize(txOut.Value, txOut.ScriptPubKey);
		}

		/// <summary>
		/// If scriptpubkey is already present, just add the value.
		/// </summary>
		public static void AddRangeWithOptimize(this TxOutList me, IEnumerable<TxOut> collection)
		{
			foreach (var txOut in collection)
			{
				me.AddWithOptimize(txOut);
			}
		}

		public static SchnorrPubKey GetSchnorrPubKey(this Signer signer) => new SchnorrPubKey(signer);

		public static uint256 BlindMessage(this Requester requester, uint256 messageHash, SchnorrPubKey schnorrPubKey) => requester.BlindMessage(messageHash, schnorrPubKey.RpubKey, schnorrPubKey.SignerPubKey);

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

		public static BitcoinWitPubKeyAddress TransformToNetworkNetwork(this BitcoinWitPubKeyAddress me, Network desiredNetwork)
		{
			Network originalNetwork = me.Network;

			if (originalNetwork == desiredNetwork)
			{
				return me;
			}

			var newAddress = new BitcoinWitPubKeyAddress(me.Hash, desiredNetwork);

			return newAddress;
		}

		public static void SortByAmount(this TxInList list, List<Coin> coins)
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

		public class TransactionDependencyNode
		{
			public List<TransactionDependencyNode> Children = new List<TransactionDependencyNode>();
			public List<TransactionDependencyNode> Parents = new List<TransactionDependencyNode>();
			public Transaction Transaction { get; set; }
		}

		public static IEnumerable<TransactionDependencyNode>ToDependencyGraph(this IEnumerable<Transaction> txs)
		{
			var lookup = new Dictionary<uint256, TransactionDependencyNode>();
			foreach(var tx in txs)
			{
				lookup.Add(tx.GetHash(), new TransactionDependencyNode { Transaction = tx });
			}

			foreach (var node in lookup.Values)
			{
				foreach(var input in node.Transaction.Inputs)
				{
					if (lookup.TryGetValue(input.PrevOut.Hash, out var parent))
					{
						if(!node.Parents.Contains(parent))
						{
							node.Parents.Add(parent);
						}
						if(!parent.Children.Contains(node))
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
					parentCounter.Add(node, node.Parents.Count());
					foreach(var child in node.Children)
					{
						Walk(child);
					}
				}
			}

			foreach(var root in roots)
			{
				Walk(root);
			}

			var nodes = parentCounter.Where(x => x.Value == 0).Select(x=>x.Key).Distinct().ToArray();
			while(nodes.Any())
			{
				foreach(var node in nodes)
				{
					yield return node.Transaction;
					parentCounter.Remove(node);
					foreach(var child in node.Children)
					{
						parentCounter[child] = parentCounter[child] - 1;
					} 
				}
				nodes = parentCounter.Where(x => x.Value == 0).Select(x=>x.Key).Distinct().ToArray();
			}
		}
	}
}
