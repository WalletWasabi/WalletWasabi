using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public abstract record CoinJoinResult;

public record SuccessfulCoinjoinResult(
	ImmutableList<SmartCoin> Coins,
	ImmutableList<Script> OutputScripts,
	Transaction UnsignedCoinJoin) : CoinJoinResult;

public record FailedCoinjoinResult : CoinJoinResult;

public record DisruptedCoinjoinResult(ImmutableList<SmartCoin> SignedCoins) : FailedCoinjoinResult;
