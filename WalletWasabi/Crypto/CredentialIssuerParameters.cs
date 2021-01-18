using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class CredentialIssuerParameters
	{
		public CredentialIssuerParameters(GroupElement cw, GroupElement i)
		{
			Cw = Guard.NotNullOrInfinity(nameof(cw), cw);
			I = Guard.NotNullOrInfinity(nameof(i), i);
		}

		public GroupElement Cw { get; }
		public GroupElement I { get; }
	}
}
