using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Statement
	{
		public Statement(GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			PublicPoint = CryptoGuard.NotNullOrInfinity(nameof(publicPoint), publicPoint);
			Generators = CryptoGuard.NotNullOrInfinity(nameof(generators), generators);
		}

		public Statement(GroupElement publicPoint, params GroupElement[] generators)
			: this(publicPoint, generators as IEnumerable<GroupElement>)
		{
		}

		public GroupElement PublicPoint { get; }
		public IEnumerable<GroupElement> Generators { get; }
	}
}
