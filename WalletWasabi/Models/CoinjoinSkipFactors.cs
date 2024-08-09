using NBitcoin;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Models;

public sealed class CoinjoinSkipFactors
{
	public CoinjoinSkipFactors(double daily, double weekly, double monthly)
	{
	}

	public static CoinjoinSkipFactors NoSkip => new(1, 1, 1);
	public static CoinjoinSkipFactors FromString(string str) =>
		NoSkip;

	public bool ShouldSkipRoundRandomly(WasabiRandom random, FeeRate roundFeeRate, uint256? roundId = null) =>
		false;
}
