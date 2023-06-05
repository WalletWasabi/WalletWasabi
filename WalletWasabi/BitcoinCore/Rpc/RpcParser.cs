using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc.Models;

namespace WalletWasabi.BitcoinCore.Rpc;

public static class RpcParser
{
	public static RpcPubkeyType ConvertPubkeyType(string? pubKeyType)
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
			"witness_v1_taproot" => RpcPubkeyType.TxWitnessV1Taproot,
			"witness_unknown" => RpcPubkeyType.TxWitnessUnknown,
			_ => RpcPubkeyType.Unknown
		};
	}

	public static VerboseBlockInfo ParseVerboseBlockResponse(string getBlockResponse)
	{
		var parsed = JsonDocument.Parse(getBlockResponse).RootElement;
		if (!parsed.TryGetProperty("result", out JsonElement blockInfoJson))
		{
			blockInfoJson = parsed;
		}

		string? previousBlockHash = null;
		if (blockInfoJson.TryGetProperty("previousblockhash", out var prevBlockHashJson))
		{
			previousBlockHash = prevBlockHashJson.GetString();
		}

		var transactions = new List<VerboseTransactionInfo>();

		var blockInfo = new VerboseBlockInfo(
			hash: uint256.Parse(blockInfoJson.GetProperty("hash").GetString()),
			prevBlockHash: previousBlockHash is not null ? uint256.Parse(previousBlockHash) : uint256.Zero,
			confirmations: blockInfoJson.GetProperty("confirmations").GetUInt64(),
			height: blockInfoJson.GetProperty("height").GetUInt64(),
			blockTime: Utils.UnixTimeToDateTime(blockInfoJson.GetProperty("time").GetUInt32()),
			transactions: transactions);

		var array = blockInfoJson.GetProperty("tx").EnumerateArray().ToArray();
		for (uint i = 0; i < array.Length; i++)
		{
			var txJson = array[i];
			var inputs = new List<VerboseInputInfo>();
			var outputs = new List<VerboseOutputInfo>();
			var txBlockInfo = new TransactionBlockInfo(blockInfo.Hash, blockInfo.BlockTime, i);
			var tx = new VerboseTransactionInfo(txBlockInfo, uint256.Parse(txJson.GetProperty("txid").GetString()), inputs, outputs);

			foreach (var txInJson in txJson.GetProperty("vin").EnumerateArray())
			{
				VerboseInputInfo input;
				if (txInJson.TryGetProperty("coinbase", out JsonElement cb))
				{
					input = new VerboseInputInfo(cb.GetString() ?? "");
				}
				else
				{
					var prevOut = txInJson.GetProperty("prevout");
					var scriptPubKey = prevOut.GetProperty("scriptPubKey");
					input = new VerboseInputInfo(
						outPoint: new OutPoint(uint256.Parse(txInJson.GetProperty("txid").GetString()), txInJson.GetProperty("vout").GetUInt32()),
						prevOutput: new VerboseOutputInfo(
							value: Money.Coins(prevOut.GetProperty("value").GetDecimal()),
							scriptPubKey: Script.FromHex(scriptPubKey.GetProperty("hex").GetString()),
							pubkeyType: scriptPubKey.GetProperty("type").GetString()));
				}

				inputs.Add(input);
			}

			foreach (var txoutJson in txJson.GetProperty("vout").EnumerateArray())
			{
				var scriptPubKey = txoutJson.GetProperty("scriptPubKey");
				var output = new VerboseOutputInfo(
					value: Money.Coins(txoutJson.GetProperty("value").GetDecimal()),
					scriptPubKey: Script.FromHex(scriptPubKey.GetProperty("hex").GetString()),
					pubkeyType: scriptPubKey.GetProperty("type").GetString());

				outputs.Add(output);
			}

			transactions.Add(tx);
		}

		return blockInfo;
	}
}
