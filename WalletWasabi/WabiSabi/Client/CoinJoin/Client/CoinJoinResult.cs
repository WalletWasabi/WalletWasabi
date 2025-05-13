using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public abstract record CoinJoinResult(
	ImmutableList<Script> OutputScripts);

public record SuccessfulCoinJoinResult(
	ImmutableList<SmartCoin> Coins,
	ImmutableList<Script> OutputScripts,
	Transaction UnsignedCoinJoin) : CoinJoinResult(OutputScripts);

public record FailedCoinJoinResult(ImmutableList<Script> OutputScripts)
	: CoinJoinResult(OutputScripts);

public record DisruptedCoinJoinResult(ImmutableList<Script> OutputScripts, ImmutableList<SmartCoin> SignedCoins)
	: CoinJoinResult(OutputScripts);
