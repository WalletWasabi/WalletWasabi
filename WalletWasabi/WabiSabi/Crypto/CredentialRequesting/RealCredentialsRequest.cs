using System.Collections.Generic;
using Newtonsoft.Json;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Extensions;
using WalletWasabi.Models;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

public record RealCredentialsRequest : ICredentialsRequest
{
	[JsonConstructor]
	public RealCredentialsRequest(
		long delta,
		IEnumerable<CredentialPresentation> presented,
		IEnumerable<IssuanceRequest> requested,
		IEnumerable<Proof> proofs)
	{
		Delta = delta;
		Presented = presented.ToImmutableValueSequence();
		Requested = requested.ToImmutableValueSequence();
		Proofs = proofs.ToImmutableValueSequence();
	}

	public long Delta { get; }

	public ImmutableValueSequence<CredentialPresentation> Presented { get; }

	public ImmutableValueSequence<IssuanceRequest> Requested { get; }

	public ImmutableValueSequence<Proof> Proofs { get; }
}
