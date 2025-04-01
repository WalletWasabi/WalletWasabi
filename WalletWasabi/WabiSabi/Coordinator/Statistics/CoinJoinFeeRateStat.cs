using NBitcoin;

namespace WalletWasabi.WabiSabi.Coordinator.Statistics;

public record CoinJoinFeeRateStat(
	DateTimeOffset DateTimeOffset,
	uint ConfirmationTarget,
	FeeRate FeeRate)
{
	public string ToLine()
	{
		return $"{DateTimeOffset.ToUnixTimeSeconds()},{ConfirmationTarget},{FeeRate.FeePerK.Satoshi}";
	}

	public static CoinJoinFeeRateStat FromLine(string line)
	{
		var parts = line.Split(",");
		var date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0]));
		var confirmationTarget = uint.Parse(parts[1]);
		var feeRate = new FeeRate(Money.Satoshis(long.Parse(parts[2])));
		return new(date, confirmationTarget, feeRate);
	}
}
