using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public record Bob(
		Script Script,
		/// <summary>
		/// This is slightly larger than the final TXO amount,
		/// because the fees are coming down from this.
		/// </summary>
		long CredentialAmount)
	{
		public int OutputVsize
			=> Script.EstimateOutputVsize();

		public Money CalculateOutputAmount(FeeRate feeRate)
			=> CredentialAmount - feeRate.GetFee(OutputVsize);

		public long CalculateWeight()
			=> Constants.WitnessScaleFactor * OutputVsize;
	}
}
