using NBitcoin;

namespace WalletWasabi.WebClients.PayJoin;

public class PayjoinClientParameters
{
	public Money? MaxAdditionalFeeContribution { get; set; }
	public FeeRate? MinFeeRate { get; set; }
	public int? AdditionalFeeOutputIndex { get; set; }
	public bool DisableOutputSubstitution { get; set; }
	public int Version { get; set; } = 1;
}
