using System.Diagnostics.CodeAnalysis;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class CredentialPresentation
	{
		[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Crypto naming")]
		public CredentialPresentation(GroupElement Ca, GroupElement Cx0, GroupElement Cx1, GroupElement CV, GroupElement S)
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

		public GroupElement ComputeZ(CoordinatorSecretKey sk)
			=> CV - (sk.W * Generators.Gw + sk.X0 * Cx0 + sk.X1 * Cx1 + sk.Ya * Ca);
	}
}
