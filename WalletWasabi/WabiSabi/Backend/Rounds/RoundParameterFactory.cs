using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class RoundParameterFactory
{
	public RoundParameterFactory(WabiSabiConfig config, Network network)
	{
		Config = config;
		Network = network;
	}

	public WabiSabiConfig Config { get; }
	public Network Network { get; }

	public virtual RoundParameters CreateRoundParameter(FeeRate feeRate, Money maxSuggestedAmount) =>
		RoundParameters.Create(
			Config,
			Network,
			feeRate,
			maxSuggestedAmount);

	public virtual RoundParameters CreateBlameRoundParameter(FeeRate feeRate, Round blameOf) =>
		RoundParameters.Create(
			Config,
			Network,
			feeRate,
			blameOf.Parameters.MaxSuggestedAmount);
}
