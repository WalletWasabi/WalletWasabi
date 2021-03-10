using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Proof
	{
		[JsonConstructor]
		internal Proof(GroupElementVector publicNonces, ScalarVector responses)
		{
			_ = Guard.NotNullOrInfinity(nameof(publicNonces), publicNonces);
			_ = Guard.NotNullOrEmpty(nameof(responses), responses);

			PublicNonces = publicNonces;
			Responses = responses;
		}

		public GroupElementVector PublicNonces { get; }
		public ScalarVector Responses { get; }
	}
}
