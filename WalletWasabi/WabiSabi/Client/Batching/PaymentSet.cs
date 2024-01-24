using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a set (or subset) of pending payments. This is an auxiliary class that is
// useful to keep the code of the PaymentAwareOutputProvider cleaner.
public record PaymentSet(IEnumerable<Payment> Payments, FeeRate MiningFeeRate)
{
	public static readonly PaymentSet Empty = new(Enumerable.Empty<Payment>(), FeeRate.Zero);

	public Money TotalAmount { get; } = Payments.Sum(x => x.EffectiveCost(MiningFeeRate));
	public int TotalVSize { get; } = Payments.Sum(x => x.Destination.ScriptPubKey.EstimateOutputVsize());
	public int PaymentCount { get; } = Payments.Count();
}
