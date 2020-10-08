using NBitcoin;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.Crypto.Api
{
	public interface ICredentialRequestBuilder
	{
		ICredentialRequestBuilder RequestCredentialFor(Money amount);

		ICredentialRequestBuilder PresentCredentials(params Credential[] credential);

		RegistrationRequest Build();
	}
}