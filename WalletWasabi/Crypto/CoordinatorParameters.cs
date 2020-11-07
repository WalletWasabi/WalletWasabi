using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class CoordinatorParameters
	{
		public CoordinatorParameters(GroupElement cw, GroupElement i)
		{
			Cw = Guard.NotNullOrInfinity(nameof(cw), cw);
			I = Guard.NotNullOrInfinity(nameof(i), i);
		}

		public GroupElement Cw { get; }
		public GroupElement I { get; }
	}
}
