using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class BlameRound : Round
{
	public BlameRound(RoundParameters roundParameters, RoundState blameOf, ISet<OutPoint> blameWhitelist)
		: base(roundParameters)
	{
		BlameOf = blameOf;
		BlameWhitelist = blameWhitelist;
		InputRegistrationTimeFrame = TimeFrame.Create(RoundParameters.BlameInputRegistrationTimeout).StartNow();
	}

	public RoundState BlameOf { get; }
	public ISet<OutPoint> BlameWhitelist { get; }

	public override bool IsInputRegistrationEnded(int maxInputCount)
	{
		return base.IsInputRegistrationEnded(BlameWhitelist.Count);
	}
}
