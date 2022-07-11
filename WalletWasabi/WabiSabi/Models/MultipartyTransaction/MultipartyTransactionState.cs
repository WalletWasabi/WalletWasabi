using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public interface IEvent{};

public record RoundCreated(RoundParameters RoundParameters) : IEvent;
public record InputAdded (Coin Coin) : IEvent;
public record OutputAdded (TxOut Output) : IEvent;

public abstract record MultipartyTransactionState
{
	protected MultipartyTransactionState(RoundParameters parameters)
	{
		var builder = ImmutableList.CreateBuilder<IEvent>();
		builder.Add(new RoundCreated(parameters));
		Events = builder.ToImmutable();
	}

	[JsonIgnore]
	public RoundParameters Parameters => Events.OfType<RoundCreated>().Single().RoundParameters;

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

	// With no coordinator fees we can't ensure that the shared overhead
	// of the transaction also pays at the nominal feerate so this will have
	// to do for now, but in the future EstimatedVsize should be used
	// including the shared overhead
	[JsonIgnore]
	public FeeRate EffectiveFeeRate => new(Balance, EstimatedInputsVsize + OutputsVsize);

	public ImmutableList<IEvent> Events { get; init; } = ImmutableList<IEvent>.Empty;

	public MultipartyTransactionState GetStateFrom(int stateId) =>
		this with {
			Events = Events.Skip(stateId).ToImmutableList()
		};

	public MultipartyTransactionState Merge(MultipartyTransactionState diff) =>
		this with {
			Events = Events.AddRange(diff.Events)
		};

	public MultipartyTransactionState AddPreviousStates(MultipartyTransactionState origin) =>
		this with {
			Events = origin.Events.AddRange(Events)
		};
}
