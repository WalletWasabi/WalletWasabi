using WalletWasabi.WabiSabi.Models;
namespace WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;

public class RoundEndedUserPressedPause : RoundEnded
{
	public RoundEndedUserPressedPause(RoundState lastRoundState) : base(lastRoundState)
	{
	}
}
