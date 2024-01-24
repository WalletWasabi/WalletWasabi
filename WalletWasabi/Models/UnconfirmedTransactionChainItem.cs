using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Models;
public record UnconfirmedTransactionChainItem(
	uint256 TxId,
	int Size,
	Money Fee,
	HashSet<uint256> Parents,
	HashSet<uint256> Children);
