using System.Collections.Immutable;
using NBitcoin;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a payment that is waiting to be made. 
public record PendingPayment(IDestination Destination, Money Amount)
	: Payment(Destination, Amount)
{
	public Guid Id { get; } = Guid.NewGuid();
	
	public bool FitParameters(ImmutableSortedSet<ScriptType> allowedOutputTypes, MoneyRange allowedOutputAmounts) =>
		allowedOutputTypes.Contains(Destination.ScriptPubKey.GetScriptType()) &&
		allowedOutputAmounts.Contains(Amount);

	public InProgressPayment ToInprogressPayment(uint256 roundId) =>
		new (Destination, Amount, roundId);
}
