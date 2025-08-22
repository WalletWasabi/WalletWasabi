using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

public interface IEvent;

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

	public RoundParameters Parameters => Events.OfType<RoundCreated>().Single().RoundParameters;

	public IEnumerable<Coin> Inputs => Events.OfType<InputAdded>().Select(x => x.Coin);
	public IEnumerable<TxOut> Outputs => Events.OfType<OutputAdded>().Select(x => x.Output);

	public Money Balance => Inputs.Sum(x => x.Amount) - Outputs.Sum(x => x.Value);
	public int EstimatedInputsVsize => Inputs.Sum(x => x.TxOut.ScriptPubKey.EstimateInputVsize());
	public int OutputsVsize => Outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());

	public int EstimatedVsize => MultipartyTransactionParameters.SharedOverhead + EstimatedInputsVsize + OutputsVsize;

	public int UnpaidSharedOverhead { get; init; } = MultipartyTransactionParameters.SharedOverhead;

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

	public MultipartyTransactionState AddPreviousStates(MultipartyTransactionState origin, uint256 roundId)
	{
		VerifyOwnershipProofs(origin, Events, roundId);
		return this with
		{
			Events = origin.Events.AddRange(Events)
		};
	}

	private void VerifyOwnershipProofs(MultipartyTransactionState state, ImmutableList<IEvent> events, uint256 roundId)
	{
		var coinJoinInputCommitData = new CoinJoinInputCommitmentData(state.Parameters.CoordinationIdentifier, roundId);
		var anyInvalidInput =
			events.OfType<InputAdded>().Any(x => !OwnershipProof.VerifyCoinJoinInputProof(x.OwnershipProof, x.Coin.ScriptPubKey, coinJoinInputCommitData));

		if (anyInvalidInput)
		{
			throw new InvalidOperationException(
				"The coordinator is cheating by adding inputs to rounds that were created to be registered in different rounds.");
		}
	}
}
