using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoinProfiles;
public static class CoinJoinTimeFrames
{
	public static readonly TimeFrameItem[] TimeFrames =
	[
		new TimeFrameItem("Hours", TimeSpan.Zero),
		new TimeFrameItem("Days", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[0])),
		new TimeFrameItem("Weeks", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[1])),
		new TimeFrameItem("Months", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[2]))
	];

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
