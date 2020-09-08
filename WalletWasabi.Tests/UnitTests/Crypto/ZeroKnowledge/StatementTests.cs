using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class StatementTests
	{
		[Fact]
		public void Throws()
		{
			// Demonstrate when it shouldn't throw.
			new LegacyStatement(Generators.G, Generators.Ga);
			new LegacyStatement(Generators.G, Generators.Ga, Generators.Gg);

			// Cannot miss generators.
			Assert.ThrowsAny<ArgumentException>(() => new LegacyStatement(Generators.G));

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new LegacyStatement(GroupElement.Infinity, Generators.Ga));
			Assert.ThrowsAny<ArgumentException>(() => new LegacyStatement(Generators.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => new LegacyStatement(GroupElement.Infinity, Generators.Ga, Generators.Gg));
			Assert.ThrowsAny<ArgumentException>(() => new LegacyStatement(Generators.G, GroupElement.Infinity, Generators.Gg));
			Assert.ThrowsAny<ArgumentException>(() => new LegacyStatement(Generators.G, Generators.Ga, GroupElement.Infinity));
		}
	}
}
