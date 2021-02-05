using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputsRegistrationRequest
	{
		public InputsRegistrationRequest(Guid roundId, IEnumerable<InputRoundSignaturePair> inputRoundSignaturePairs, RegistrationRequestMessage zeroAmountCredentialRequests, RegistrationRequestMessage zeroWeightCredentialRequests)
		{
			RoundId = roundId;
			InputRoundSignaturePairs = inputRoundSignaturePairs;
			ZeroAmountCredentialRequests = zeroAmountCredentialRequests;
			ZeroWeightCredentialRequests = zeroWeightCredentialRequests;

			if (!ZeroAmountCredentialRequests.IsNullRequest)
			{
				throw new InvalidOperationException("Only zero credentials can be requested.");
			}
			if (!ZeroWeightCredentialRequests.IsNullRequest)
			{
				throw new InvalidOperationException("Only zero credentials can be requested.");
			}
		}

		public Guid RoundId { get; }
		public IEnumerable<InputRoundSignaturePair> InputRoundSignaturePairs { get; }
		public RegistrationRequestMessage ZeroAmountCredentialRequests { get; }
		public RegistrationRequestMessage ZeroWeightCredentialRequests { get; }
	}
}
