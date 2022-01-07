using NBitcoin;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public abstract record MultipartyTransactionState
{
	protected MultipartyTransactionState(MultipartyTransactionParameters parameters)
	{
		Parameters = parameters;
	}

	public MultipartyTransactionParameters Parameters { get; }

	public ImmutableList<Coin> Inputs { get; init; } = ImmutableList<Coin>.Empty;
	public ImmutableList<TxOut> Outputs { get; init; } = ImmutableList<TxOut>.Empty;

	public Money Balance => Inputs.Sum(x => x.Amount) - Outputs.Sum(x => x.Value);
	public int EstimatedInputsVsize => Inputs.Sum(x => x.TxOut.ScriptPubKey.EstimateInputVsize());
	public int OutputsVsize => Outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());

	public int EstimatedVsize => MultipartyTransactionParameters.SharedOverhead + EstimatedInputsVsize + OutputsVsize;
	public int MaxTransactionSize => Parameters.MaxTransactionSize;

	// With no coordinator fees we can't ensure that the shared overhead
	// of the transaction also pays at the nominal feerate so this will have
	// to do for now, but in the future EstimatedVsize should be used
	// including the shared overhead
	public FeeRate EffectiveFeeRate => new(Balance, EstimatedInputsVsize + OutputsVsize);
}
