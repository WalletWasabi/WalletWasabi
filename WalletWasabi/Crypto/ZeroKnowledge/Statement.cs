using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Statement
	{
		public Statement(GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			PublicPoint = publicPoint;
			Generators = generators;
		}

		public GroupElement PublicPoint { get; }
		public IEnumerable<GroupElement> Generators { get; }
	}
}
