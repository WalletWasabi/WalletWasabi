using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public class OutputRegistrationRequest
	{
		public OutputRegistrationRequest(Guid roundId, Script script, RealCredentialsRequest amountCredentialRequests, RealCredentialsRequest weightCredentialRequests)
		{
			RoundId = roundId;
			Script = script;
			AmountCredentialRequests = amountCredentialRequests;
			WeightCredentialRequests = weightCredentialRequests;
		}

		public Guid RoundId { get; }
		public Script Script { get; }
		public RealCredentialsRequest AmountCredentialRequests { get; }
		public RealCredentialsRequest WeightCredentialRequests { get; }
	}
}
