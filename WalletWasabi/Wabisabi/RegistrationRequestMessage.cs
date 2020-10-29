using System.Linq;
using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.Wabisabi
{
	/// <summary>
	/// Represents a request message for the WabiSabi unified registration protocol.
	/// </summary>
	/// <remarks>
	/// RegistrationRequestMessage message is the unified protocol message used in both phases,
	/// inputs registration and outputs registration and it is designed to supports
	/// credentials reissuance.
	/// </remarks>
	public class RegistrationRequestMessage
	{
		internal RegistrationRequestMessage(
			Money balance, 
			IEnumerable<CredentialPresentation> presented, 
			IEnumerable<IssuanceRequest> requested, 
			IEnumerable<Proof> proofs)
		{
			DeltaAmount = balance;
			Presented = presented;
			Requested = requested;
			Proofs = proofs;
		}

		/// <summary>
		/// The difference between the sum of the requested credentials and the presented credentials.
		/// </summary>
		/// <remarks>
		/// A positive value of this property indicates that the request is an inputs registration request,
		/// a negative value indicates it is a outputs registration requests while finally a zero value
		/// indicates it is a reissuance request or well a request for zero-value credentials. 
		/// </remarks>
		public Money DeltaAmount { get; }

		/// <summary>
		/// Randomized credentials that will be presented for output registration or reissuance.
		/// </summary>
		public IEnumerable<CredentialPresentation> Presented { get; }
		
		/// <summary>
		/// Credential isssuance requests.
		/// </summary>
		public IEnumerable<IssuanceRequest> Requested { get; }
		
		/// <summary>
		/// Accompanying range and sum proofs to the coordinator.
		/// </summary>
		public IEnumerable<Proof> Proofs { get; }

		/// <summary>
		/// Is request for zero-value credentials only.
		/// </summary>
		public bool IsNullRequest => DeltaAmount == Money.Zero && !Presented.Any();
		
		/// <summary>
		/// Serial numbers used in the credential presentations.
		/// </summary>
		public IEnumerable<GroupElement> SerialNumbers => Presented.Select(x => x.S);

		internal bool AreThereDuplicatedSerialNumbers => SerialNumbers.Distinct().Count() < SerialNumbers.Count();
	}
}