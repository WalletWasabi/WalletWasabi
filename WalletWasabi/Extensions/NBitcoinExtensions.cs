using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
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

			using (var listener = node.CreateListener())
			{
				var getdata = new GetDataPayload(new InventoryVector(node.AddSupportedOptions(InventoryType.MSG_BLOCK), hash));
				await node.SendMessageAsync(getdata);
				cancellationToken.ThrowIfCancellationRequested();

				// Bitcoin Core processes the messages sequentially and does not send a NOTFOUND message if the remote node is pruned and the data not available.
				// A good way to get any feedback about whether the node knows the block or not is to send a ping request.
				// If block is not known by the remote node, the pong will be sent immediately, else it will be sent after the block download.
				ulong pingNonce = RandomUtils.GetUInt64();
				await node.SendMessageAsync(new PingPayload() { Nonce = pingNonce });
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
			return equalOutputs;
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

		public static uint256 BlindScript(this Requester requester, PubKey signerPubKey, PubKey RPubKey, Script script)
		{
			var msg = new uint256(Hashes.SHA256(script.ToBytes()));
			return requester.BlindMessage(msg, RPubKey, signerPubKey);
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
			foreach (var txout in collection)
			{
				me.AddWithOptimize(txout);
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

		public static async Task StopAsync(this RPCClient rpc)
		{
			await rpc.SendCommandAsync("stop");
		}

		public static SmartTransaction ExtractSmartTransaction(this PSBT psbt)
		{
			return psbt.ExtractSmartTransaction(Height.Unknown);
		}

		public static SmartTransaction ExtractSmartTransaction(this PSBT psbt, Height height, uint256 blockHash = null, int blockIndex = 0, string label = "", DateTimeOffset? firstSeenIfMemPoolTime = null, bool isReplacement = false)
		{
			var extractedTx = psbt.ExtractTransaction();
			return new SmartTransaction(extractedTx, height, blockHash, blockIndex, label, firstSeenIfMemPoolTime, isReplacement);
		}

		public static void SortByAmount(this TxOutList list)
		{
			list.Sort((x, y) => x.Value.CompareTo(y.Value));
		}
	}
}
