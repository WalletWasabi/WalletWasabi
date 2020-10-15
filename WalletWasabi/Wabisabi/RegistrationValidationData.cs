using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.Wabisabi
{
	public class RegistrationValidationData
	{
		public RegistrationValidationData(Transcript transcript, IEnumerable<Credential> presented, IssuanceValidationData[] requested)
		{
			Transcript = transcript;
			Presented = presented; 
			Requested = requested;
		}

		public Transcript Transcript { get; }

		public IEnumerable<Credential> Presented { get; }
		
		public IssuanceValidationData[] Requested { get; }
	}
}
