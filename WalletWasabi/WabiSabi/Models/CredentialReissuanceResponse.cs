using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class CredentialReissuanceResponse
	{
		public CredentialReissuanceResponse(RegistrationResponseMessage zeroCredentials, RegistrationResponseMessage realCredentials)
		{
			ZeroCredentials = zeroCredentials;
			RealCredentials = realCredentials;
		}

		public RegistrationResponseMessage ZeroCredentials { get; }

		public RegistrationResponseMessage RealCredentials { get; }
	}
}
