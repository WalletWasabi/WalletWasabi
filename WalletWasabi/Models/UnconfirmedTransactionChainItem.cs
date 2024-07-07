using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.Models;

// This class is here for backward compatibility and should be removed on removal of unconfirmed-transaction-chain endpoint.
public record UnconfirmedTransactionChainItem(
	uint256 TxId,
	int Size,
	Money Fee,
	HashSet<uint256> Parents,
	HashSet<uint256> Children);
