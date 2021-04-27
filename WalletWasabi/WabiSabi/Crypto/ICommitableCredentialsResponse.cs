using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Crypto
{
	public interface ICommitableCredentialsResponse
	{
		CredentialsResponse Commit();
	}
}
