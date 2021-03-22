using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record ConnectionConfirmationRequest(
		Guid RoundId,
		Guid AliceId, 
		ZeroCredentialsRequest ZeroAmountCredentialRequests, 
		RealCredentialsRequest RealAmountCredentialRequests, 
		ZeroCredentialsRequest ZeroWeightCredentialRequests, 
		RealCredentialsRequest RealWeightCredentialRequests
	);
}
