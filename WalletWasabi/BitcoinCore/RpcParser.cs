using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using WalletWasabi.BitcoinCore.RpcModels;

namespace WalletWasabi.BitcoinCore
{
	public class RpcParser
	{
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
					VerboseInputInfo input = null;

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
								scriptPubKey: Script.FromHex(txinJson["prevout"]["scriptPubKey"].Value<string>("hex")))
						);
					}

					inputs.Add(input);
				}

				foreach (var txoutJson in txJson["vout"])
				{
					var output = new VerboseOutputInfo(
						value: Money.Coins(txoutJson.Value<decimal>("value")),
						scriptPubKey: Script.FromHex(txoutJson["scriptPubKey"].Value<string>("hex"))
					);

					outputs.Add(output);
				}

				transaction.Add(tx);
			}

			return blockInfo;
		}
	}
}
