using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public class ConnectionConfirmationRequest
	{
		public ConnectionConfirmationRequest(Guid roundId, Guid aliceId, ZeroCredentialsRequest zeroAmountCredentialRequests, RealCredentialsRequest realAmountCredentialRequests, ZeroCredentialsRequest zeroWeightCredentialRequests, RealCredentialsRequest realWeightCredentialRequests)
		{
			RoundId = roundId;
			AliceId = aliceId;
			ZeroAmountCredentialRequests = zeroAmountCredentialRequests;
			RealAmountCredentialRequests = realAmountCredentialRequests;
			ZeroWeightCredentialRequests = zeroWeightCredentialRequests;
			RealWeightCredentialRequests = realWeightCredentialRequests;
		}

		public Guid RoundId { get; }
		public Guid AliceId { get; }
		public ZeroCredentialsRequest ZeroAmountCredentialRequests { get; }
		public RealCredentialsRequest RealAmountCredentialRequests { get; }
		public ZeroCredentialsRequest ZeroWeightCredentialRequests { get; }
		public RealCredentialsRequest RealWeightCredentialRequests { get; }
	}
}
