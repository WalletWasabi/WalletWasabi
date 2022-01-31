using NBitcoin;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

	public ImmutableList<MultipartyTransactionState> PreviousStates { get; init; } = ImmutableList<MultipartyTransactionState>.Empty;

	public MultipartyTransactionState GetConstructionStateSince(int order)
	{
		var state = PreviousStates.IsEmpty || order >= PreviousStates.Count
			? this
			: PreviousStates[order < 0 ? 0 : order];

		return this with {
			Inputs = Inputs.GetRange(state.Inputs.Count, Inputs.Count - state.Inputs.Count),
			Outputs = Outputs.GetRange(state.Outputs.Count, Outputs.Count - state.Outputs.Count)
		};
	}

	public MultipartyTransactionState Merge(MultipartyTransactionState diff) =>
		this with {
			Inputs = Inputs.AddRange(diff.Inputs),
			Outputs = Outputs.AddRange(diff.Outputs),
			PreviousStates = diff.PreviousStates,
		};

	public MultipartyTransactionState MergeBack(MultipartyTransactionState origin) =>
		this with {
			Inputs = origin.Inputs.AddRange(Inputs),
			Outputs = origin.Outputs.AddRange(Outputs),
		};
}
