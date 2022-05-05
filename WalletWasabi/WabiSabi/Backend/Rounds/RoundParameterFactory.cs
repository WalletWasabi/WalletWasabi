using System.Collections.Generic;
using System.Threading;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public class RoundParameterFactory
{
	public RoundParameterFactory(WabiSabiConfig config, Network network)
	{
		Config = config;
		Network = network;
		MaxSuggestedAmountProvider = new (Config);
	}

	public WabiSabiConfig Config { get; }
	public Network Network { get; }
	public MaxSuggestedAmountProvider MaxSuggestedAmountProvider { get; }
	
	public virtual RoundParameters CreateRoundParameter(FeeRate feeRate, int connectionConfirmationStartedCounter) =>
		RoundParameters.Create(
			Config,
			Network,
			feeRate,
			Config.CoordinationFeeRate,
			MaxSuggestedAmountProvider.GetMaxSuggestedAmount(connectionConfirmationStartedCounter));

	public virtual RoundParameters CreateBlameRoundParameter(FeeRate feeRate, Round blameOf) =>
		RoundParameters.Create(
			Config,
			Network,
			feeRate,
			Config.CoordinationFeeRate,
			blameOf.Parameters.MaxSuggestedAmount);
}