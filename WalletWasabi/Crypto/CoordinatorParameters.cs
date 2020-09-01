using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto
{
	public class CoordinatorParameters
	{
		public CoordinatorParameters(GroupElement Cw, GroupElement I)
		{
			this.Cw = CryptoGuard.NotInfinity(nameof(Cw), Cw);
			this.I = CryptoGuard.NotInfinity(nameof(I), I);
		}

		public GroupElement Cw { get; }
		public GroupElement I { get; }
	}
}
