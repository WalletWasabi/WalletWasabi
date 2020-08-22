using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class KnowledgeOfAndTests
	{
		[Theory]
		[InlineData(1, 1, 1, 2)]
		[InlineData(1, 2, 3, 5)]
		[InlineData(3, 5, 5, 7)]
		[InlineData(5, 7, 7, 11)]
		[InlineData(7, 11, short.MaxValue, uint.MaxValue)]
		[InlineData(short.MaxValue, uint.MaxValue, int.MaxValue, uint.MaxValue)]
		[InlineData(int.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue)]
		[InlineData(uint.MaxValue, uint.MaxValue, 1, 2)]
		public void End2EndVerificationSimple(uint scalarSeed1, uint scalarSeed2, uint scalarSeed3, uint scalarSeed4)
		{
			var secrets = new[] { new Scalar(scalarSeed1), new Scalar(scalarSeed2) };
			var generators = new[] { Generators.G, Generators.Ga };

			var secrets2 = new[] { new Scalar(scalarSeed3), new Scalar(scalarSeed4) };
			var generators2 = new[] { Generators.Gg, Generators.Gh };

			var publicPoint = GroupElement.Infinity;
			var secretGeneratorPairs = secrets.ZipForceEqualLength(generators);
			foreach (var (secret, generator) in secretGeneratorPairs)
			{
				publicPoint += secret * generator;
			}
			var statement1 = new Statement(publicPoint, generators);

			var publicPoint2 = GroupElement.Infinity;
			var secretGeneratorPairs2 = secrets2.ZipForceEqualLength(generators2);
			foreach (var (secret, generator) in secretGeneratorPairs2)
			{
				publicPoint2 += secret * generator;
			}
			var statement2 = new Statement(publicPoint2, generators2);

			var proof = Prover.CreateAndProof(new[]
			{
				new KnowledgeOfRepParams(secrets, statement1),
				new KnowledgeOfRepParams(secrets2, statement2)
			});
			Assert.True(Verifier.Verify(proof, new[] { new Statement(publicPoint, generators), new Statement(publicPoint2, generators2) }));
		}

		[Fact]
		public void Throws()
		{
			// Demonstrate when it shouldn't throw.
			var kor = new KnowledgeOfRep(Generators.G, CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo);
			var kor2 = new KnowledgeOfRep(Generators.Ga, CryptoHelpers.ScalarLarge, CryptoHelpers.ScalarTwo);
			new KnowledgeOfAnd(new[] { kor, kor2 });

			// Array cannot be empty or 1.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfAnd(Enumerable.Empty<KnowledgeOfRep>()));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfAnd(new[] { kor }));
		}
	}
}
