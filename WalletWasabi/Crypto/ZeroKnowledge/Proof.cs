using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Proof
	{
		public Proof(GroupElementVector publicNonces, ScalarVector responses)
		{
			CryptoGuard.NotNullOrInfinity(nameof(publicNonces), publicNonces);
			Guard.NotNullOrEmpty(nameof(responses), responses);

			PublicNonces = publicNonces;
			Responses = responses;
		}

		public GroupElementVector PublicNonces { get; }
		public ScalarVector Responses { get; }
	}
}
