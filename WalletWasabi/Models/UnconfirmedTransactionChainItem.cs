using System.Collections.Generic;
using System.Text.Json.Serialization;
using NBitcoin;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi.Models;
public record UnconfirmedTransactionChainItem(
	[property: JsonPropertyName("txId")] string TxId,
	[property: JsonPropertyName("size")] int Size,
	[property: JsonPropertyName("fee")]
	[property: JsonConverter(typeof(MoneyBtcJsonConverter))] Money Fee,
	[property: JsonPropertyName("parents")] HashSet<string> Parents,
	[property: JsonPropertyName("children")] HashSet<string> Children);
