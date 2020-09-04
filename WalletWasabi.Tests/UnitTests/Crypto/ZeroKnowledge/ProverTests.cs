using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	// public class ProverTests
	// {
	// 	[Fact]
	// 	public void RandomOverflow()
	// 	{
	// 		var mockRandom = new MockRandom();
	// 		foreach (var scalar in CryptoHelpers.GetScalars(x => x.IsOverflow))
	// 		{
	// 			mockRandom.GetScalarResults.Add(scalar);

	// 			Assert.ThrowsAny<InvalidOperationException>(() => Prover.CreateProof(new KnowledgeOfDlogParams(Scalar.One, new Statement(Scalar.One * Generators.G, Generators.G)), mockRandom));
	// 		}
	// 	}

	// 	[Fact]
	// 	public void RandomZero()
	// 	{
	// 		var mockRandom = new MockRandom();
	// 		mockRandom.GetScalarResults.Add(Scalar.Zero);

	// 		// Don't tolerate if the second zero scalar random is received.
	// 		mockRandom.GetScalarResults.Add(Scalar.Zero);

	// 		Assert.ThrowsAny<InvalidOperationException>(() => Prover.CreateProof(new KnowledgeOfDlogParams(Scalar.One, new Statement(Scalar.One * Generators.G, Generators.G)), mockRandom));
	// 	}
	// }
}
