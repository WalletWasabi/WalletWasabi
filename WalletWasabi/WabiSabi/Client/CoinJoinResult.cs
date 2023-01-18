using System.Collections.Generic;
using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public abstract record CoinJoinResult
{
}

public record SuccessfulCoinjoin(
	ImmutableList<SmartCoin> Coins,
	ImmutableList<Script> OutputScripts,
	Transaction UnsignedCoinJoin) : CoinJoinResult
{
}

public record DisruptedCoinjoin(
	ImmutableList<SmartCoin> SignedCoins) : CoinJoinResult
{
}

public record FailedCoinjoin() : CoinJoinResult
{
}

