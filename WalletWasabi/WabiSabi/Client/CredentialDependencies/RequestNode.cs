using System.Collections.Generic;
using System.Collections.Immutable;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	// make private inner class of Graph?
	public class RequestNode
	{
		public RequestNode(IEnumerable<long> values, int inDegree, int outDegree, int zeroOnlyOutDegree)
		{
			Values = Guard.InRange(nameof(values), values, DependencyGraph.K, DependencyGraph.K).ToImmutableArray();
			MaxInDegree = inDegree;
			MaxOutDegree = outDegree;
			MaxZeroOnlyOutDegree = zeroOnlyOutDegree;
		}

		public ImmutableArray<long> Values { get; }

		// TODO ImmutableArray<int> (uint?)
		public int MaxInDegree { get; }

		public int MaxOutDegree { get; }

		public int MaxZeroOnlyOutDegree { get; }

		public long InitialBalance(CredentialType type) => Values[(int)type];
	}
}
