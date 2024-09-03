using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies;

public enum CredentialType
{
	Amount,
	Vsize
}

public abstract record RequestNode(Guid Id, long Amount, long Vsize, int MaxInDegree, int MaxOutDegree, int MaxZeroOnlyOutDegree);


public record InputNode(long Amount, long Vsize)
	: RequestNode(Guid.NewGuid(), Amount, Vsize, 0, DependencyGraph.K, DependencyGraph.K * (DependencyGraph.K - 1));

public record OutputNode(long Amount, long Vsize)
	: RequestNode(Guid.NewGuid(), Amount, Vsize, DependencyGraph.K, 0, 0);

public record ReissuanceNode()
	: RequestNode(
		Guid.NewGuid(),
		0,
		0,
		DependencyGraph.K,
		DependencyGraph.K,
		DependencyGraph.K * (DependencyGraph.K - 1));

public record CredentialDependency(Guid Id, RequestNode From, RequestNode To, long Value);
