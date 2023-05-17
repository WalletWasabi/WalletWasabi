using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies;

public class OutputNode : RequestNode
{
	public OutputNode(IEnumerable<long> values) : base(values, DependencyGraph.K, 0, 0)
	{
	}

	public Money EffectiveCost => Money.Satoshis(Math.Abs(InitialBalance(CredentialType.Amount)));
}
