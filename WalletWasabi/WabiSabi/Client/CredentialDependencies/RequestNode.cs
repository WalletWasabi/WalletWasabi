using System.Collections.Immutable;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	// make private inner class of Graph?
	public class RequestNode
	{
		public RequestNode(ImmutableArray<long> values)
		{
			Guard.InRange(nameof(values), values, DependencyGraph.K, DependencyGraph.K);
			Values = values;
		}

		public ImmutableArray<long> Values { get; }
		public long InitialBalance(CredentialType type) => Values[(int)type];
	}
}
