using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record CoinJoinFeeRateMedian(TimeSpan TimeFrame, FeeRate MedianFeeRate);
