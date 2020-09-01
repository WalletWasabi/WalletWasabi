using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto
{
	public class CoordinatorParameters
	{
		public CoordinatorParameters(GroupElement cw, GroupElement i)
		{
			Cw = CryptoGuard.NotNullOrInfinity(nameof(cw), cw);
			I = CryptoGuard.NotNullOrInfinity(nameof(i), i);
		}

		public GroupElement Cw { get; }
		public GroupElement I { get; }
	}
}
