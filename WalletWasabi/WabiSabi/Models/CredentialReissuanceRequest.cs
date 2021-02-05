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
		public CredentialReissuanceRequest(Guid roundId, RegistrationRequestMessage zeroCredentialRequests, RegistrationRequestMessage realCredentialRequests)
		{
			RoundId = roundId;
			ZeroCredentialRequests = zeroCredentialRequests;
			RealCredentialRequests = realCredentialRequests;
		}

		public Guid RoundId { get; }
		public RegistrationRequestMessage ZeroCredentialRequests { get; }
		public RegistrationRequestMessage RealCredentialRequests { get; }
	}
}
