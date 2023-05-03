using WalletWasabi.Extensions;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Client.Batching;

// Represents a payment (dst, amount). 
public abstract record Payment(IDestination Destination, Money Amount)
{
	public Money EffectiveCost(FeeRate feeRate) =>
		Amount + feeRate.GetFee(Destination.ScriptPubKey.EstimateOutputVsize());
}