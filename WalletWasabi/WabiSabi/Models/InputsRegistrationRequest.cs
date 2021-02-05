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
		public InputsRegistrationRequest(Guid roundId, IEnumerable<InputRoundSignaturePair> inputRoundSignaturePairs, RegistrationRequestMessage zeroCredentialRequests)
		{
			RoundId = roundId;
			InputRoundSignaturePairs = inputRoundSignaturePairs;
			ZeroCredentialRequests = zeroCredentialRequests;
		}

		public Guid RoundId { get; }
		public IEnumerable<InputRoundSignaturePair> InputRoundSignaturePairs { get; }
		public RegistrationRequestMessage ZeroCredentialRequests { get; }
	}
}
