using System.Collections.Generic;
using Newtonsoft.Json;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Extensions;
using WalletWasabi.Models;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

public record ZeroCredentialsRequest : ICredentialsRequest
{
	[JsonConstructor]
	public ZeroCredentialsRequest(
		IEnumerable<IssuanceRequest> requested,
		IEnumerable<Proof> proofs)
	{
		Requested = requested.ToImmutableValueSequence();
		Proofs = proofs.ToImmutableValueSequence();
	}

	public long Delta => 0;

	public ImmutableValueSequence<CredentialPresentation> Presented => ImmutableValueSequence<CredentialPresentation>.Empty;

	public ImmutableValueSequence<IssuanceRequest> Requested { get; }

	public ImmutableValueSequence<Proof> Proofs { get; }
}
