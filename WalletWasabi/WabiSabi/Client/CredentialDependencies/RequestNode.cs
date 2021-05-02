using System.Collections.Immutable;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	// make private inner class of Graph?
	public record RequestNode(int Id, ImmutableArray<long> Values)
	{
		public long InitialBalance(CredentialType type) => Values[(int)type];
	}
}
