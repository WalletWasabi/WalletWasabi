using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class ConnectionConfirmationRequest
	{
		public ConnectionConfirmationRequest(Guid roundId, Guid aliceId, RegistrationRequestMessage zeroCredentialRequests, RegistrationRequestMessage realCredentialRequests)
		{
			RoundId = roundId;
			AliceId = aliceId;
			RealCredentialRequests = realCredentialRequests;
			ZeroCredentialRequests = zeroCredentialRequests;
		}

		public Guid RoundId { get; }

		public Guid AliceId { get; }
		public RegistrationRequestMessage RealCredentialRequests { get; }
		public RegistrationRequestMessage ZeroCredentialRequests { get; }
	}
}
