using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record CoinJoinFeeRateAvarage(int TimeFrameHours, FeeRate AvarageFeeRate);
