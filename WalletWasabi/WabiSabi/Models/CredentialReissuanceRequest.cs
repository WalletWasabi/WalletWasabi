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
		public CredentialReissuanceRequest(Guid roundId, IEnumerable<RegistrationRequestMessage> zeroCredentialRequests, IEnumerable<RegistrationRequestMessage> realCredentialRequests)
		{
			RoundId = roundId;
			ZeroCredentialRequests = zeroCredentialRequests;
			RealCredentialRequests = realCredentialRequests;
		}

		public Guid RoundId { get; }
		public IEnumerable<RegistrationRequestMessage> ZeroCredentialRequests { get; }
		public IEnumerable<RegistrationRequestMessage> RealCredentialRequests { get; }
	}
}
