using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.Models;

// This class is here for backward compatibility and should be removed on removal of unconfirmed-transaction-chain endpoint.
public record UnconfirmedTransactionChainItem(
	[JsonProperty]
	uint256 TxId,
	[JsonProperty]
	int Size,
	[JsonProperty]
	Money Fee,
	[JsonProperty]
	HashSet<uint256> Parents,
	[JsonProperty]
	HashSet<uint256> Children);
