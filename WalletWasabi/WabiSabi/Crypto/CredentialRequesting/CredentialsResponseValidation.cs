using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

/// <summary>
/// Maintains the state needed to validate the credentials once the coordinator
/// issues them.
/// </summary>
public record CredentialsResponseValidation
{
	internal CredentialsResponseValidation(
		Transcript transcript,
		IEnumerable<Credential> presented,
		IEnumerable<IssuanceValidationData> requested)
	{
		Transcript = transcript;
		Presented = presented;
		Requested = requested;
	}

	/// <summary>
	/// The transcript in the correct state that must be used to validate the proofs presented by the coordinator.
	/// </summary>
	internal Transcript Transcript { get; }

	/// <summary>
	/// The credentials that were presented to the coordinator.
	/// </summary>
	public IEnumerable<Credential> Presented { get; }

	/// <summary>
	/// The data state that has to be used to validate the issued credentials.
	/// </summary>
	public IEnumerable<IssuanceValidationData> Requested { get; }
}
