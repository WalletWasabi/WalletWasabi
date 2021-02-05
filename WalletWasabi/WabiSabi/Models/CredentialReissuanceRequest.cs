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
		public CredentialReissuanceRequest(Guid roundId, RegistrationRequestMessage amountCredentialRequests, RegistrationRequestMessage weighCredentialRequests)
		{
			RoundId = roundId;
			AmountCredentialRequests = amountCredentialRequests;
			WeighCredentialRequests = weighCredentialRequests;
		}

		public Guid RoundId { get; }
		public RegistrationRequestMessage AmountCredentialRequests { get; }
		public RegistrationRequestMessage WeighCredentialRequests { get; }
	}
}
