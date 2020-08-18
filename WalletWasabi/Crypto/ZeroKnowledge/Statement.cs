using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public class Statement
	{
		public Statement(GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			foreach (var generator in generators)
			{
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			}

			PublicPoint = publicPoint;
			Generators = Guard.NotNullOrEmpty(nameof(generators), generators);
		}

		public Statement(GroupElement publicPoint, params GroupElement[] generators)
			: this(publicPoint, generators as IEnumerable<GroupElement>)
		{
		}

		public GroupElement PublicPoint { get; }
		public IEnumerable<GroupElement> Generators { get; }
	}
}
