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
			DeltaAmount = balance;
			Presented = presented;
			Requested = requested;
			Proofs = proofs;
		}

		public Money DeltaAmount { get; }

		public IEnumerable<CredentialPresentation> Presented { get; }
		
		public IEnumerable<CredentialIssuanceRequest> Requested { get; }
		
		public IEnumerable<Proof> Proofs { get; }

		public bool IsNullRequest => DeltaAmount == Money.Zero && !Presented.Any();
		
		public IEnumerable<GroupElement> SerialNumbers => Presented.Select(x => x.S);
	} 
}