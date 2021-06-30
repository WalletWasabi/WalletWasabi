using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	public class InputNode : RequestNode
	{
		public InputNode(IEnumerable<long> values) : base(values, 0, DependencyGraph.K, DependencyGraph.K * (DependencyGraph.K - 1))
		{
		}

		public Money EffectiveValue => Money.Satoshis(InitialBalance(CredentialType.Amount));

		public int VsizeRemainingAllocation => (int)InitialBalance(CredentialType.Vsize);
	}
}
