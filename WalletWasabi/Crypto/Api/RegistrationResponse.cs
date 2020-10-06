using System;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;

namespace WalletWasabi.Crypto.Api
{
	public class RegistrationResponse
	{
		public RegistrationResponse(IEnumerable<MAC> issuedCredentials, IEnumerable<Proof> proofs)
		{
			IssuedCredentials = issuedCredentials;
			Proofs = proofs;
		}

		public IEnumerable<MAC> IssuedCredentials { get; }

		public IEnumerable<Proof> Proofs { get; }
	}
}