using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	public class Proof
	{
		public Proof(GroupElementVector publicNonces, IEnumerable<ScalarVector> allResponses)
		{
			CryptoGuard.NotNullOrInfinity(nameof(publicNonces), publicNonces);
			Guard.NotNullOrEmpty(nameof(allResponses), allResponses);

			// Ensure allResponses isn't jagged
			var n = allResponses.First().Count();
			Guard.True(nameof(allResponses), allResponses.All(responses => Guard.NotNullOrEmpty(nameof(responses), responses).Count() == n));

			// Ensure there is a vector of response scalars for each public nonce
			Guard.True(nameof(allResponses), allResponses.Count() == publicNonces.Count());

			PublicNonces = publicNonces;
			Responses = allResponses;
		}

		public GroupElementVector PublicNonces { get; }
		public IEnumerable<ScalarVector> Responses { get; }
	}
}
