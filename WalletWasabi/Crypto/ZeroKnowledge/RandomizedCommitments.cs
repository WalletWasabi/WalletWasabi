using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class RandomizedCommitments
	{
		public RandomizedCommitments(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV)
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