using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Crypto
{
	public partial class CredentialIssuer
	{
		public interface ICommitableCredentialsResponse
		{
			CredentialsResponse Commit();
		}
	}
}
