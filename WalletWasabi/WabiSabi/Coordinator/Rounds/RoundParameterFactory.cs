using NBitcoin;

namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public delegate RoundParameters RoundParameterCreator(FeeRate feeRate, Money maxSuggestedAmount);
public delegate RoundParameters BlameRoundParameterCreator(FeeRate feeRate, Round blameOf);

public class RoundParameterFactory
{
	private readonly RoundParameterCreator _createRoundParameterCreator;
	private readonly BlameRoundParameterCreator _createBlameRoundParameterCreator;

	public RoundParameterFactory(
		WabiSabiConfig config,
		RoundParameterCreator? createRoundParameterCreator = null,
		BlameRoundParameterCreator? createBlameRoundParameterCreator = null)
	{
		Config = config;
		_createRoundParameterCreator = createRoundParameterCreator ?? DefaultCreateRoundParameters;
		_createBlameRoundParameterCreator = createBlameRoundParameterCreator ?? DefaultBlameRoundParameters;
	}

	public WabiSabiConfig Config { get; }

	public RoundParameters CreateRoundParameter(FeeRate feeRate, Money maxSuggestedAmount) =>
		_createRoundParameterCreator(feeRate, maxSuggestedAmount);

	public RoundParameters CreateBlameRoundParameter(FeeRate feeRate, Round blameOf) =>
		_createBlameRoundParameterCreator(feeRate, blameOf);

	private RoundParameters DefaultCreateRoundParameters(FeeRate feeRate, Money maxSuggestedAmount) =>
		RoundParameters.Create(Config, feeRate, maxSuggestedAmount);

	private RoundParameters DefaultBlameRoundParameters(FeeRate feeRate, Round blameOf) =>
		RoundParameters.Create(Config, feeRate, blameOf.Parameters.MaxSuggestedAmount);
}
