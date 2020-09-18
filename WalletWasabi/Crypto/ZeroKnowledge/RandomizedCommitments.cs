using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class RandomizedCommitments
	{
		private RandomizedCommitments(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV, GroupElement S, GroupElement Z)
		{
			this.Ca = Ca;
			this.Cx0 = Cx0;
			this.Cx1 = Cx1;
			this.CV = CV;
			this.S = S;
			this.Z = Z;
		}

		public GroupElement Ca { get; }
		public GroupElement Cx0 { get; }
		public GroupElement Cx1 { get; }
		public GroupElement CV { get; }
		public GroupElement S { get; }
		public GroupElement Z { get; }

		public static RandomizedCommitments RandomizeMAC(MAC mac, Scalar z, Scalar a, Scalar r, CoordinatorParameters iparams)
		{
			GroupElement Randomize(GroupElement G, GroupElement M) => M + z * G;
			return new RandomizedCommitments(
				Ca: Randomize(Generators.Ga, a * Generators.Gg + r * Generators.Gh),
				Cx0: Randomize(Generators.Gx0, mac.U),
				Cx1: Randomize(Generators.Gx1, mac.T * mac.U),
				CV: Randomize(Generators.GV, mac.V),
				S: r * Generators.Gs,
				Z: z * iparams.I);
		}

		public static RandomizedCommitments RandomizeMAC(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV, GroupElement S, CoordinatorSecretKey sk)
			=> new RandomizedCommitments(Ca, Cx0, Cx1, CV, S, Z: CV - (sk.W * Generators.Gw + sk.X0 * Cx0 + sk.X1 * Cx1 + sk.Ya * Ca));
	}
}
