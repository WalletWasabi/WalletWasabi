namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	// make private inner class of Graph class?
	public record CredentialDependency(RequestNode From, RequestNode To, CredentialType CredentialType, ulong Value);
}
