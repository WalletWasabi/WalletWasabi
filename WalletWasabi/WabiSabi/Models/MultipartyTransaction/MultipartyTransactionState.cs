using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public interface IEvent
{ }

public record RoundCreated(RoundParameters RoundParameters) : IEvent;
public record InputAdded(Coin Coin, OwnershipProof OwnershipProof) : IEvent;
public record OutputAdded(TxOut Output) : IEvent;

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

	[JsonIgnore]
	public Money EstimatedCost => Parameters.MiningFeeRate.GetFee(EstimatedVsize - UnpaidSharedOverhead);

	[JsonIgnore]
	public int UnpaidSharedOverhead { get; init; } = MultipartyTransactionParameters.SharedOverhead;

	[JsonIgnore]
	public FeeRate EffectiveFeeRate => new(Balance, EstimatedVsize - UnpaidSharedOverhead);

	public ImmutableList<IEvent> Events { get; init; } = ImmutableList<IEvent>.Empty;

	public MultipartyTransactionState GetStateFrom(int stateId) =>
		this with
		{
			Events = Events.Skip(stateId).ToImmutableList()
		};

	public MultipartyTransactionState Merge(MultipartyTransactionState diff) =>
		this with
		{
			Events = Events.AddRange(diff.Events)
		};

	public MultipartyTransactionState AddPreviousStates(MultipartyTransactionState origin) =>
		this with
		{
			Events = origin.Events.AddRange(Events)
		};
}
