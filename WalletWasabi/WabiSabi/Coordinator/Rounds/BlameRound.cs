using System.Collections.Generic;
using NBitcoin;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public class BlameRound : Round
{
	public BlameRound(RoundParameters parameters, Round blameOf, ISet<OutPoint> blameWhitelist, WasabiRandom random)
		: base(parameters, random)
	{
		BlameOf = blameOf;
		BlameWhitelist = blameWhitelist;
		InputRegistrationTimeFrame = TimeFrame.Create(Parameters.BlameInputRegistrationTimeout).StartNow();
	}

	public Round BlameOf { get; }
	public ISet<OutPoint> BlameWhitelist { get; }
}
