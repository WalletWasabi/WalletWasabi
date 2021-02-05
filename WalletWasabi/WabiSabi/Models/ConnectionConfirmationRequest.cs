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
		public ConnectionConfirmationRequest(Guid roundId, Guid aliceId, RegistrationRequestMessage zeroAmountCredentialRequests, RegistrationRequestMessage realAmountCredentialRequests, RegistrationRequestMessage realWeightCredentialRequests, RegistrationRequestMessage zeroWeightCredentialRequests)
		{
			RoundId = roundId;
			AliceId = aliceId;
			RealAmountCredentialRequests = realAmountCredentialRequests;
			ZeroAmountCredentialRequests = zeroAmountCredentialRequests;
			RealWeightCredentialRequests = realWeightCredentialRequests;
			ZeroWeightCredentialRequests = zeroWeightCredentialRequests;
		}

		public Guid RoundId { get; }

		public Guid AliceId { get; }
		public RegistrationRequestMessage RealAmountCredentialRequests { get; }
		public RegistrationRequestMessage ZeroAmountCredentialRequests { get; }
		public RegistrationRequestMessage RealWeightCredentialRequests { get; }
		public RegistrationRequestMessage ZeroWeightCredentialRequests { get; }
	}
}
