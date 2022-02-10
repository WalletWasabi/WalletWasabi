using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public record FeeRateStatRecord(
	DateTimeOffset DateTimeOffset,
	uint ConfirmationTarget,
	FeeRate FeeRate)
{
	public string ToLine()
	{
		return $"{DateTimeOffset.ToUnixTimeSeconds()},{ConfirmationTarget},{(uint)Math.Ceiling(FeeRate.SatoshiPerByte)}";
	}

	public static FeeRateStatRecord FromLine(string line)
	{
		var parts = line.Split(",");
		var date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0]));
		var confirmationTarget = uint.Parse(parts[1]);
		var feeRate = new FeeRate((decimal)uint.Parse(parts[2]));
		return new(date, confirmationTarget, feeRate);
	}
}
