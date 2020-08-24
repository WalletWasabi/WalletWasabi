using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class ChallengeTests
	{
		[Fact]
		public void BuildThrows()
		{
			// Demonstrate when it shouldn't throw.
			Statement statement1 = new Statement(Generators.Ga, Generators.Gg);
			Statement statement2 = new Statement(Generators.Ga, Generators.Gg, Generators.Gh);
			Challenge.Build(Generators.G, statement1);
			Challenge.Build(Generators.G, statement2);

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(GroupElement.Infinity, statement1));
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(GroupElement.Infinity, statement2));

			// Public and random points cannot be the same.
			Assert.ThrowsAny<InvalidOperationException>(() => Challenge.Build(Generators.G, new Statement(Generators.G, Generators.Gg)));
			Assert.ThrowsAny<InvalidOperationException>(() => Challenge.Build(Generators.G, new Statement(Generators.G, Generators.Gg, Generators.Gh)));
		}
	}
}
