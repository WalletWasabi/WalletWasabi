using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public interface IEvent{};
public record InputAdded (Coin Coin) : IEvent;
public record OutputAdded (TxOut Output) : IEvent;

public abstract record MultipartyTransactionState
{
	protected MultipartyTransactionState(MultipartyTransactionParameters parameters)
	{
		Parameters = parameters;
	}

	public MultipartyTransactionParameters Parameters { get; }

	[JsonIgnore]
	public IEnumerable<Coin> Inputs => Events.OfType<InputAdded>().Select(x => x.Coin);
	[JsonIgnore]
	public IEnumerable<TxOut> Outputs => Events.OfType<OutputAdded>().Select(x => x.Output);

	[JsonIgnore]
	public Money Balance => Inputs.Sum(x => x.Amount) - Outputs.Sum(x => x.Value);
	[JsonIgnore]
	public int EstimatedInputsVsize => Inputs.Sum(x => x.TxOut.ScriptPubKey.EstimateInputVsize());
	[JsonIgnore]
	public int OutputsVsize => Outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());

	[JsonIgnore]
	public int EstimatedVsize => MultipartyTransactionParameters.SharedOverhead + EstimatedInputsVsize + OutputsVsize;
	[JsonIgnore]
	public int MaxTransactionSize => Parameters.MaxTransactionSize;

	// With no coordinator fees we can't ensure that the shared overhead
	// of the transaction also pays at the nominal feerate so this will have
	// to do for now, but in the future EstimatedVsize should be used
	// including the shared overhead
	[JsonIgnore]
	public FeeRate EffectiveFeeRate => new(Balance, EstimatedInputsVsize + OutputsVsize);

	public ImmutableList<IEvent> Events { get; init; } = ImmutableList<IEvent>.Empty;

	public MultipartyTransactionState GetConstructionStateSince(int order) =>
		this with {
			Events = Events.Skip(order).ToImmutableList()
		};

	public MultipartyTransactionState Merge(MultipartyTransactionState diff) =>
		this with {
			Events = Events.AddRange(diff.Events)
		};

	public MultipartyTransactionState MergeBack(MultipartyTransactionState origin) =>
		this with {
			Events = origin.Events.AddRange(Events)
		};
}
