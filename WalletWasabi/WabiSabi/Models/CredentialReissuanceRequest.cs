using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class CredentialReissuanceRequest
	{
		public CredentialReissuanceRequest(IEnumerable<RegistrationRequestMessage> zeroCredentialRequests, IEnumerable<RegistrationRequestMessage> realCredentialRequests)
		{
			ZeroCredentialRequests = zeroCredentialRequests;
			RealCredentialRequests = realCredentialRequests;
		}

		public IEnumerable<RegistrationRequestMessage> ZeroCredentialRequests { get; }
		public IEnumerable<RegistrationRequestMessage> RealCredentialRequests { get; }
	}
}
