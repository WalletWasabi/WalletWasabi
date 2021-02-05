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
		public CredentialReissuanceRequest(Guid roundId, RegistrationRequestMessage credentialRequests)
		{
			RoundId = roundId;
			CredentialRequests = credentialRequests;
		}

		public Guid RoundId { get; }
		public RegistrationRequestMessage CredentialRequests { get; }
	}
}
