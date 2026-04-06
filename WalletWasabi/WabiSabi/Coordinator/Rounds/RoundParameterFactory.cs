using NBitcoin;

namespace WalletWasabi.WabiSabi.Coordinator.Rounds;

public class RoundParameterFactory
{
	private readonly Func<FeeRate, Money, RoundParameters>? _createRoundParameterFn;
	private readonly Func<FeeRate, Round, RoundParameters>? _createBlameRoundParameterFn;

	public RoundParameterFactory(WabiSabiConfig config, Network network) : this(config, network, createRoundParameterFn: null, createBlameRoundParameterFn: null)
	{
	}

	public RoundParameterFactory(
		WabiSabiConfig config,
		Network network,
		Func<FeeRate, Money, RoundParameters>? createRoundParameterFn = null,
		Func<FeeRate, Round, RoundParameters>? createBlameRoundParameterFn = null)
	{
		Config = config;
		Network = network;
		_createRoundParameterFn = createRoundParameterFn;
		_createBlameRoundParameterFn = createBlameRoundParameterFn;
	}

	public WabiSabiConfig Config { get; }
	public Network Network { get; }

	public virtual RoundParameters CreateRoundParameter(FeeRate feeRate, Money maxSuggestedAmount)
	{
		if (_createRoundParameterFn is not null)
		{
			return _createRoundParameterFn(feeRate, maxSuggestedAmount);
		}

		return RoundParameters.Create(
			Config,
			Network,
			feeRate,
			maxSuggestedAmount);
	}

	public virtual RoundParameters CreateBlameRoundParameter(FeeRate feeRate, Round blameOf)
	{
		if (_createBlameRoundParameterFn is not null)
		{
			return _createBlameRoundParameterFn(feeRate, blameOf);
		}

		return RoundParameters.Create(
			Config,
			Network,
			feeRate,
			blameOf.Parameters.MaxSuggestedAmount);
	}
}
