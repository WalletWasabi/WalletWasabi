namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public abstract record CoinJoinResult;

public record SuccessfulCoinJoinResult(
	ImmutableList<SmartCoin> Coins,
	ImmutableList<Script> OutputScripts,
	Transaction UnsignedCoinJoin,
	Money MiningFee,
	Money WastedDust) : CoinJoinResult;

public record FailedCoinJoinResult : CoinJoinResult;

public record DisruptedCoinJoinResult(
	ImmutableList<SmartCoin> MySignedCoins,
	ImmutableArray<Coin> AllRoundCoins,
	Money MaxSuggestedAmount,
	FeeRate MiningFeeRate) : CoinJoinResult;
