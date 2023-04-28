using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.Batching;

public record PendingPayment(IDestination Destination, Money Amount)
{
	public TxOut ToTxOut() =>
		new (Amount, Destination);

	public Money EffectiveCost(FeeRate feeRate) =>
		ToTxOut().EffectiveCost(feeRate);
}

public record PaymentSet(IEnumerable<PendingPayment> Payments, FeeRate MiningFeeRate)
{
	public static readonly PaymentSet Empty = new(Enumerable.Empty<PendingPayment>(), FeeRate.Zero);

	public Money TotalAmount { get; } = Payments.Sum(x => x.EffectiveCost(MiningFeeRate));
	public int TotalVSize { get; } = Payments.Sum(x => x.Destination.ScriptPubKey.EstimateOutputVsize());
	public int PaymentCount { get; } = Payments.Count();
}