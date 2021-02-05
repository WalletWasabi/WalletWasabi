using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class ConnectionConfirmationResponse
	{
		public ConnectionConfirmationResponse(RegistrationResponseMessage zeroAmountCredentials, RegistrationResponseMessage zeroWeightCredentials, RegistrationResponseMessage? realAmountCredentials = null, RegistrationResponseMessage? realWeightCredentials = null)
		{
			ZeroAmountCredentials = zeroAmountCredentials;
			ZeroWeightCredentials = zeroWeightCredentials;
			RealAmountCredentials = realAmountCredentials;
			RealWeightCredentials = realWeightCredentials;
		}

		public RegistrationResponseMessage ZeroAmountCredentials { get; }
		public RegistrationResponseMessage ZeroWeightCredentials { get; }
		public RegistrationResponseMessage? RealAmountCredentials { get; }
		public RegistrationResponseMessage? RealWeightCredentials { get; }
	}
}
