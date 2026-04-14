using NBitcoin;

namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public delegate RoundParameters RoundParametersCreator(FeeRate feeRate, Money maxSuggestedAmount);
public delegate RoundParameters BlameRoundParametersCreator(FeeRate feeRate, Round blameOf);

public class RoundParametersFactory
{
	private readonly RoundParametersCreator _createRoundParametersCreator;
	private readonly BlameRoundParametersCreator _createBlameRoundParametersCreator;

	public RoundParametersFactory(
		WabiSabiConfig config,
		RoundParametersCreator? createRoundParameterCreator = null,
		BlameRoundParametersCreator? createBlameRoundParameterCreator = null)
	{
		Config = config;
		_createRoundParametersCreator = createRoundParameterCreator ?? DefaultCreateRoundParameters;
		_createBlameRoundParametersCreator = createBlameRoundParameterCreator ?? DefaultBlameRoundParameters;
	}

	public WabiSabiConfig Config { get; }

	public RoundParameters CreateRoundParameters(FeeRate feeRate, Money maxSuggestedAmount) =>
		_createRoundParametersCreator(feeRate, maxSuggestedAmount);

	public RoundParameters CreateBlameRoundParameters(FeeRate feeRate, Round blameOf) =>
		_createBlameRoundParametersCreator(feeRate, blameOf);

	private RoundParameters DefaultCreateRoundParameters(FeeRate feeRate, Money maxSuggestedAmount) =>
		RoundParameters.Create(Config, feeRate, maxSuggestedAmount);

	private RoundParameters DefaultBlameRoundParameters(FeeRate feeRate, Round blameOf) =>
		RoundParameters.Create(Config, feeRate, blameOf.Parameters.MaxSuggestedAmount);
}
