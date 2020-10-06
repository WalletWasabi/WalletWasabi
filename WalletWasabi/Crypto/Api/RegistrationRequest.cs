using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;

namespace WalletWasabi.Crypto.Api
{
	public class RegistrationRequest
	{
		internal RegistrationRequest(
			Money balance, 
			IEnumerable<CredentialPresentation> presented, 
			IEnumerable<CredentialIssuanceRequest> requested, 
			IEnumerable<Proof> proofs)
		{
			Balance = balance;
			Presented = presented;
			Requested = requested;
			Proofs = proofs;
		}

		public Money Balance { get; }
		public IEnumerable<CredentialPresentation> Presented { get; }
		public IEnumerable<CredentialIssuanceRequest> Requested { get; }
		public IEnumerable<Proof> Proofs { get; }

		public bool IsNullRequest => Balance == Money.Zero && !Presented.Any();
	} 
}