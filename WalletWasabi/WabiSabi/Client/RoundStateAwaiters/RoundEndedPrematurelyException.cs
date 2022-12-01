using NBitcoin;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class RoundEndedPrematurelyException : InvalidOperationException
{
	public RoundEndedPrematurelyException(uint256 roundId, Exception? innerException = null) : base(null, innerException)
	{
		RoundId = roundId;
	}

	public uint256 RoundId { get; }
}
