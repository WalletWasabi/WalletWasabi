using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto
{
	public class CoordinatorParameters
	{
		public CoordinatorParameters(GroupElement cw, GroupElement i)
		{
			Cw = CryptoGuard.NotInfinity(nameof(cw), cw);
			I = CryptoGuard.NotInfinity(nameof(i), i);
		}

		public GroupElement Cw { get; }
		public GroupElement I { get; }
	}
}
