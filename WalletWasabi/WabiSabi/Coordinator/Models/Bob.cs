using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Coordinator.Models;

/// <param name="CredentialAmount"> This is slightly larger than the final TXO amount,because the fees are coming down from this.</param>
public record Bob(Script Script, long CredentialAmount)
{
	public int OutputVsize
		=> Script.EstimateOutputVsize();

	public Money CalculateOutputAmount(FeeRate feeRate)
		=> CredentialAmount - feeRate.GetFee(OutputVsize);
}
