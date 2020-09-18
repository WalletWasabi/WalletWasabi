using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class RandomizedCommitments
	{
		private RandomizedCommitments(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV, GroupElement S)
		{
			this.Ca = Ca;
			this.Cx0 = Cx0;
			this.Cx1 = Cx1;
			this.CV = CV;
			this.S = S;
		}

		public GroupElement Ca { get; }
		public GroupElement Cx0 { get; }
		public GroupElement Cx1 { get; }
		public GroupElement CV { get; }
		public GroupElement S { get; }

		public static RandomizedCommitments RandomizeMAC(MAC mac, Scalar z, Scalar a, Scalar r)
		{
			GroupElement Randomize(GroupElement G, GroupElement M) => M + z * G;
			return new RandomizedCommitments(
				Ca: Randomize(Generators.Ga, a * Generators.Gg + r * Generators.Gh),
				Cx0: Randomize(Generators.Gx0, mac.U),
				Cx1: Randomize(Generators.Gx1, mac.T * mac.U),
				CV: Randomize(Generators.GV, mac.V),
				S: r * Generators.Gs);
		}

		public GroupElement ComputeZ(CoordinatorSecretKey sk)
			=> CV - (sk.W * Generators.Gw + sk.X0 * Cx0 + sk.X1 * Cx1 + sk.Ya * Ca);
	}
}