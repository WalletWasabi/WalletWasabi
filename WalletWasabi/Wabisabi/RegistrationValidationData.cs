using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.Wabisabi
{
	/// <summary>
	/// Maintains the state needed to validate the credentials once the coordinator
	/// issues them.
	/// </summary>
	public class RegistrationValidationData
	{
		internal RegistrationValidationData(
			Transcript transcript, 
			IEnumerable<Credential> presented,
			IEnumerable<IssuanceValidationData> requested)
		{
			Transcript = transcript;
			Presented = presented;
			Requested = requested;
		}

		/// <summary>
		/// The transcript in the correct state that must be use to validate the proofs presented by the coordinator.
		/// </summary>
		public Transcript Transcript { get; }

		/// <summary>
		/// The credentials that were presented to the coordinator.
		/// </summary>
		public IEnumerable<Credential> Presented { get; }
		
		/// <summary>
		/// The data state that has to be use to validate the issued credentials.
		/// </summary>
		public IEnumerable<IssuanceValidationData> Requested { get; }
	}
}
