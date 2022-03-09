using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public record CoinJoinResult(
	bool GoForBlameRound,
	bool SuccessfulBroadcast,
	ImmutableList<SmartCoin> RegisteredCoins,
	ImmutableList<Script> RegisteredOutputs)
{
	public CoinJoinResult(bool goForBlameRound) :
		this(goForBlameRound, false, ImmutableList<SmartCoin>.Empty, ImmutableList<Script>.Empty)
	{
	}
}
