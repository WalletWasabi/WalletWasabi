using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public record CoinJoinResult(
	bool GoForBlameRound,
	bool SuccessfulBroadcast,
	ImmutableList<SmartCoin> RegisteredCoins,
	ImmutableList<Script> RegisteredOutputs,
	ImmutableDictionary<TxOut, PendingPayment> HandledPayments,
	Transaction UnsignedCoinJoin, uint256 RoundId);
