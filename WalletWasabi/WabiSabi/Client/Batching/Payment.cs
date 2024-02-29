using System.Collections.Immutable;
using WalletWasabi.Extensions;
using NBitcoin;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.Batching;

public abstract record PaymentState
{
	protected PaymentState(PaymentState previousState)
	{
		PreviousState = previousState;
	}

	public PaymentState? PreviousState { get; init; }
}

public record PendingPayment(PaymentState? PreviousState) : PaymentState(PreviousState);
public record InProgressPayment(PaymentState PreviousState, uint256 RoundId) : PaymentState(PreviousState);
public record FinishedPayment(PaymentState PreviousState, uint256 TransactionId) : PaymentState(PreviousState);

public record Payment(IDestination Destination, Money Amount)
{
	public Guid Id { get; } = Guid.NewGuid();
	public PaymentState State { get; init; } = new PendingPayment(null);

	public TxOut ToTxOut() => new(Amount, Destination.ScriptPubKey);

	public Money EffectiveCost(FeeRate feeRate) =>
		Amount + feeRate.GetFee(Destination.ScriptPubKey.EstimateOutputVsize());

	public bool FitParameters(ImmutableSortedSet<ScriptType> allowedOutputTypes, MoneyRange allowedOutputAmounts) =>
		allowedOutputTypes.Contains(Destination.ScriptPubKey.GetScriptType()) &&
		allowedOutputAmounts.Contains(Amount);
}
