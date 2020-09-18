using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class RandomizedCommitments
	{
		public static RandomizedCommitments RandomizeMAC(MAC mac, Scalar z, GroupElement Ma)
		{
			GroupElement Randomize(GroupElement G, GroupElement M) => M + z * G;
			return new RandomizedCommitments(
				Ca:  Randomize(Generators.Ga, Ma),
				Cx0: Randomize(Generators.Gx0, mac.U),
				Cx1: Randomize(Generators.Gx1, mac.T * mac.U),
				CV:  Randomize(Generators.GV, mac.V));
		}

		private RandomizedCommitments(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV)
		{
			this.Ca = Ca;
			this.Cx0 = Cx0;
			this.Cx1 = Cx1;
			this.CV = CV;
		}

		public GroupElement Ca { get; }
		public GroupElement Cx0 { get; }
		public GroupElement Cx1 { get; }
		public GroupElement CV { get; }
	}
}