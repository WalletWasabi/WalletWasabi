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
		public OutputRegistrationRequest(Guid roundId, TxOut output, RealCredentialsRequest amountCredentialRequests, RealCredentialsRequest weighCredentialRequests)
		{
			RoundId = roundId;
			Output = output;
			AmountCredentialRequests = amountCredentialRequests;
			WeighCredentialRequests = weighCredentialRequests;
		}

		public Guid RoundId { get; }
		public TxOut Output { get; }
		public RealCredentialsRequest AmountCredentialRequests { get; }
		public RealCredentialsRequest WeighCredentialRequests { get; }
	}
}
