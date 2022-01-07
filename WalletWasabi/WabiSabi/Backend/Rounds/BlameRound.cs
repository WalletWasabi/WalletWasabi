using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class BlameRound : Round
{
	public BlameRound(RoundParameters roundParameters, Round blameOf, ISet<OutPoint> blameWhitelist)
		: base(roundParameters)
	{
		BlameOf = blameOf;
		BlameWhitelist = blameWhitelist;
		InputRegistrationTimeFrame = TimeFrame.Create(RoundParameters.BlameInputRegistrationTimeout).StartNow();
	}

	public Round BlameOf { get; }
	public ISet<OutPoint> BlameWhitelist { get; }

	public override bool IsInputRegistrationEnded(int maxInputCount)
	{
		return base.IsInputRegistrationEnded(BlameWhitelist.Count);
	}
}
