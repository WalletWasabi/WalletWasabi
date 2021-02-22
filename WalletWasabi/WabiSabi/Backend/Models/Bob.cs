using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class Bob
	{
		public Bob(Script script, long credentialAmount)
		{
			Script = script;
			CredentialAmount = credentialAmount;
		}

		public Script Script { get; }

		/// <summary>
		/// This is slightly larger than the final TXO amount,
		/// because the fees are coming down from this.
		/// </summary>
		public long CredentialAmount { get; }

		public int OutputVsize => Script.EstimateOutputVsize();

		public Money CalculateOutputAmount(FeeRate feeRate)
			=> CredentialAmount - feeRate.GetFee(OutputVsize);

		public long CalculateWeight() => OutputVsize * 4;
	}
}
