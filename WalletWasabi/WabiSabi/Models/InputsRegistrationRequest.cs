using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputsRegistrationRequest
	{
		public InputsRegistrationRequest(Guid roundId, IEnumerable<InputRoundSignaturePair> inputRoundSignaturePairs, ZeroCredentialsRequest zeroAmountCredentialRequests, ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			RoundId = roundId;
			InputRoundSignaturePairs = inputRoundSignaturePairs;
			ZeroAmountCredentialRequests = zeroAmountCredentialRequests;
			ZeroWeightCredentialRequests = zeroWeightCredentialRequests;
		}

		public Guid RoundId { get; }
		public IEnumerable<InputRoundSignaturePair> InputRoundSignaturePairs { get; }
		public ZeroCredentialsRequest ZeroAmountCredentialRequests { get; }
		public ZeroCredentialsRequest ZeroWeightCredentialRequests { get; }
	}
}
