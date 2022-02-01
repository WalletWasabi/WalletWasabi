using WalletWasabi.Models;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

/// <summary>
/// Represents a request message for the WabiSabi unified registration protocol.
/// </summary>
/// <remarks>
/// RegistrationRequestMessage message is the unified protocol message used in both phases,
/// inputs registration and outputs registration and it is designed to support
/// credentials reissuance.
/// </remarks>
public interface ICredentialsRequest
{
	/// <summary>
	/// The difference between the sum of the requested credentials and the presented credentials.
	/// </summary>
	/// <remarks>
	/// A positive value of this property indicates that the request is an inputs registration request,
	/// a negative value indicates it is an outputs registration request, while finally a zero value
	/// indicates it is a reissuance request or a request for zero-value credentials.
	/// </remarks>
	long Delta { get; }

	/// <summary>
	/// Randomized credentials presented for output registration or reissuance.
	/// </summary>
	ImmutableValueSequence<CredentialPresentation> Presented { get; }

	/// <summary>
	/// Credential isssuance requests.
	/// </summary>
	ImmutableValueSequence<IssuanceRequest> Requested { get; }

	/// <summary>
	/// Accompanying range and sum proofs to the coordinator.
	/// </summary>
	ImmutableValueSequence<Proof> Proofs { get; }
}
