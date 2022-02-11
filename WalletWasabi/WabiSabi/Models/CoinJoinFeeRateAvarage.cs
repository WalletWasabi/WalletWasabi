using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record CoinJoinFeeRateAvarage(TimeSpan TimeFrame, FeeRate AvarageFeeRate);
