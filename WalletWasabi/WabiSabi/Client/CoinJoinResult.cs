using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public record CoinJoinResult(
	bool GoForBlameRound,
	bool SuccessfulBroadcast,
	IEnumerable<SmartCoin> RegisteredCoins)
{
}
