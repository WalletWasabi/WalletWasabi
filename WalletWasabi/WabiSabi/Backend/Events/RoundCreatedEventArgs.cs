using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Events;

public class RoundCreatedEventArgs : EventArgs
{
	public RoundCreatedEventArgs(uint256 roundId, RoundParameters roundParameters) : base()
	{
		RoundId = roundId;
		RoundParameters = roundParameters;
	}

	public uint256 RoundId { get; }
	public RoundParameters RoundParameters { get; }
}
