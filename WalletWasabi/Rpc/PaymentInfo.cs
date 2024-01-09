using NBitcoin;

namespace WalletWasabi.Rpc;

public class PaymentInfo
{
	public required BitcoinAddress Sendto { get; init; }
	public required Money Amount { get; init; }
	public required string Label { get; init; }
	public bool SubtractFee { get; init; }
}
