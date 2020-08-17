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
			Challenge.Build(Generators.G, Generators.Ga);
			Challenge.Build(Generators.G, Generators.Ga, Generators.Gg, Generators.Gh);

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(Generators.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(GroupElement.Infinity, Generators.Ga));
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(GroupElement.Infinity, GroupElement.Infinity));

			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(GroupElement.Infinity, Generators.Ga, Generators.Gg, Generators.Gh));
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(Generators.G, GroupElement.Infinity, Generators.Gg, Generators.Gh));
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(Generators.G, Generators.Ga, GroupElement.Infinity, Generators.Gh));
			Assert.ThrowsAny<ArgumentException>(() => Challenge.Build(Generators.G, Generators.Ga, Generators.Gg, GroupElement.Infinity));

			// Public and random points cannot be the same.
			Assert.ThrowsAny<InvalidOperationException>(() => Challenge.Build(Generators.G, Generators.G));
			Assert.ThrowsAny<InvalidOperationException>(() => Challenge.Build(Generators.G, Generators.G, Generators.Gg, Generators.Gh));
		}
	}
}
