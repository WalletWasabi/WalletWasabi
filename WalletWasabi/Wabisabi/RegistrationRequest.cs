using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;

namespace WalletWasabi.Wabisabi
{
	public class RegistrationRequest
	{
		internal RegistrationRequest(
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

		public Money DeltaAmount { get; }

		public IEnumerable<CredentialPresentation> Presented { get; }
		
		public IEnumerable<IssuanceRequest> Requested { get; }
		
		public IEnumerable<Proof> Proofs { get; }

		public bool IsNullRequest => DeltaAmount == Money.Zero && !Presented.Any();
		
		public IEnumerable<GroupElement> SerialNumbers => Presented.Select(x => x.S);

		public bool AreThereDuplicatedSerialNumbers => SerialNumbers.Distinct().Count() < SerialNumbers.Count();

	} 
}