using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using WalletWasabi.BitcoinCore.Rpc.Models;

namespace WalletWasabi.BitcoinCore.Rpc
{
	public static class RpcParser
	{
		public const string SpentError1 = "bad-txns-inputs-missingorspent";
		public const string SpentError2 = "missing-inputs";
		public const string SpentError3 = "txn-mempool-conflict";
		public const string TooLongMempoolChainError = "too-long-mempool-chain";

		public const string SpentErrorTranslation = "At least one coin you are trying to spend is already spent.";

		public static bool IsSpentError(string error) => new[] { SpentError1, SpentError2, SpentError3 }.Any(x => error.Contains(x, StringComparison.OrdinalIgnoreCase));

		public static bool IsTooLongMempoolChainError(string error) => error.Contains(TooLongMempoolChainError, StringComparison.OrdinalIgnoreCase);

		public static Dictionary<string, string> ErrorTranslations { get; } = new Dictionary<string, string>
		{
			[TooLongMempoolChainError] = "At least one coin you are trying to spend is part of long or heavy chain of unconfirmed transactions. You must wait for some previous transactions to confirm.",
			[SpentError1] = SpentErrorTranslation,
			[SpentError2] = SpentErrorTranslation,
			[SpentError3] = SpentErrorTranslation,
			["bad-txns-inputs-duplicate"] = "The transaction contains duplicated inputs.",
			["bad-txns-nonfinal"] = "The transaction is not final and cannot be broadcasted.",
			["bad-txns-oversize"] = "The transaction is too big.",

			["invalid password"] = "Wrong password.",
			["Invalid wallet name"] = "Invalid wallet name.",
			["Wallet name is already taken"] = "Wallet name is already taken."
		};

		public static RpcPubkeyType ConvertPubkeyType(string pubKeyType)
		{
			return pubKeyType switch
			{
				"nonstandard" => RpcPubkeyType.TxNonstandard,
				"pubkey" => RpcPubkeyType.TxPubkey,
				"pubkeyhash" => RpcPubkeyType.TxPubkeyhash,
				"scripthash" => RpcPubkeyType.TxScripthash,
				"multisig" => RpcPubkeyType.TxMultisig,
				"nulldata" => RpcPubkeyType.TxNullData,
				"witness_v0_keyhash" => RpcPubkeyType.TxWitnessV0Keyhash,
				"witness_v0_scripthash" => RpcPubkeyType.TxWitnessV0Scripthash,
				"witness_unknown" => RpcPubkeyType.TxWitnessUnknown,
				_ => RpcPubkeyType.Unknown
			};
		}

		public static VerboseBlockInfo ParseVerboseBlockResponse(string getBlockResponse)
		{
			var blockInfoJson = JObject.Parse(getBlockResponse);
			var previousBlockHash = blockInfoJson.Value<string>("previousblockhash");
			var transaction = new List<VerboseTransactionInfo>();

			var blockInfo = new VerboseBlockInfo(
				hash: uint256.Parse(blockInfoJson.Value<string>("hash")),
				prevBlockHash: previousBlockHash is { } ? uint256.Parse(previousBlockHash) : uint256.Zero,
				confirmations: blockInfoJson.Value<ulong>("confirmations"),
				height: blockInfoJson.Value<ulong>("height"),
				blockTime: Utils.UnixTimeToDateTime(blockInfoJson.Value<uint>("time")),
				transactions: transaction
			);

			foreach (var txJson in blockInfoJson["tx"])
			{
				var inputs = new List<VerboseInputInfo>();
				var outputs = new List<VerboseOutputInfo>();
				var tx = new VerboseTransactionInfo(uint256.Parse(txJson.Value<string>("txid")), inputs, outputs);

				foreach (var txinJson in txJson["vin"])
				{
					VerboseInputInfo input;
					if (txinJson["coinbase"] is { })
					{
						input = new VerboseInputInfo(txinJson["coinbase"].Value<string>());
					}
					else
					{
						input = new VerboseInputInfo(
							outPoint: new OutPoint(uint256.Parse(txinJson.Value<string>("txid")), txinJson.Value<uint>("vout")),
							prevOutput: new VerboseOutputInfo(
								value: Money.Coins(txinJson["prevout"].Value<decimal>("value")),
								scriptPubKey: Script.FromHex(txinJson["prevout"]["scriptPubKey"].Value<string>("hex")),
								pubkeyType: txinJson["prevout"]["scriptPubKey"].Value<string>("type"))
						);
					}

					inputs.Add(input);
				}

				foreach (var txoutJson in txJson["vout"])
				{
					var output = new VerboseOutputInfo(
						value: Money.Coins(txoutJson.Value<decimal>("value")),
						scriptPubKey: Script.FromHex(txoutJson["scriptPubKey"].Value<string>("hex")),
						pubkeyType: txoutJson["scriptPubKey"].Value<string>("type")
					);

					outputs.Add(output);
				}

				transaction.Add(tx);
			}

			return blockInfo;
		}
	}
}
