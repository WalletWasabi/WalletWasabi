using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Credential
	{
		public Credential(Scalar amount, Scalar randomness, MAC mac)
		{
			Amount = amount;
			Randomness = randomness;
			Mac = mac;
		}

		public Scalar Amount { get; } // could consider uint, but only really makes sense for credential requests because of range proofs
		public Scalar Randomness { get; }
		public MAC Mac { get; }

		public CredentialPresentation Present(Scalar z)
			=> CredentialPresentation.FromMAC(Mac, z, Amount, Randomness);
	}
}
