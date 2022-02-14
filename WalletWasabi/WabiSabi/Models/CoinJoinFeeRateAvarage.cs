using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record CoinJoinFeeRateAverage(TimeSpan TimeFrame, FeeRate AverageFeeRate);
