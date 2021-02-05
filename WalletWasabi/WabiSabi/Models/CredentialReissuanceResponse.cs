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
		public CredentialReissuanceResponse(RegistrationResponseMessage credentials)
		{
			Credentials = credentials;
		}

		public RegistrationResponseMessage Credentials { get; }
	}
}
