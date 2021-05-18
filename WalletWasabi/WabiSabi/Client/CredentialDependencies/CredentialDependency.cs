namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	// make private inner class of Graph class?
	public class CredentialDependency
	{
		public CredentialDependency(RequestNode from, RequestNode to, CredentialType credentialType, ulong value)
		{
			From = from;
			To = to;
			CredentialType = credentialType;
			Value = value;
		}

		public RequestNode From { get; }
		public RequestNode To { get; }
		public CredentialType CredentialType { get; }
		public ulong Value { get; }
	}
}
