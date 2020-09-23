using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Credential
	{
		public Credential(Scalar amount, Scalar randomness, MAC mac, GroupElement ma)
		{
			Amount = amount;
			Randomness = randomness;
			Mac = mac;
			Ma = ma;
		}

		public Scalar Amount { get; }
		public Scalar Randomness { get; }
		public MAC Mac { get; }
		public GroupElement Ma { get; }

		public CredentialPresentation Present(Scalar z)
		{
			GroupElement Randomize(GroupElement G, GroupElement M) => M + z * G;
			return new CredentialPresentation(
				Ca: Randomize(Generators.Ga, Ma),
				Cx0: Randomize(Generators.Gx0, Mac.U),
				Cx1: Randomize(Generators.Gx1, Mac.T * Mac.U),
				CV: Randomize(Generators.GV, Mac.V),
				S: Randomness * Generators.Gs);
		}
	}
}
