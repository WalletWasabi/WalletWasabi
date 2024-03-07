using System.Collections.Generic;
using System.Text.Json.Serialization;
using NBitcoin;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi.Models;
public record UnconfirmedTransactionChainItem(
	[property: JsonPropertyName("txId")] uint256 TxId,
	[property: JsonPropertyName("size")] int Size,
	[property: JsonPropertyName("fee")]
	[property: JsonConverter(typeof(MoneyBtcJsonConverter))] Money Fee,
	[property: JsonPropertyName("parents")] HashSet<uint256> Parents,
	[property: JsonPropertyName("children")] HashSet<uint256> Children);
