using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	public class OutputNode : RequestNode
	{
		public OutputNode(IEnumerable<long> values) : base(values, DependencyGraph.K, 0, 0)
		{
		}

		public Money EffectiveCost => Money.Satoshis(Math.Abs(InitialBalance(CredentialType.Amount)));
	}
}
