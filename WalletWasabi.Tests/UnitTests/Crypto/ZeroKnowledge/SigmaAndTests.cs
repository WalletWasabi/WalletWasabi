using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class SigmaAndTests
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

			var publicPoint2 = GroupElement.Infinity;
			var secretGeneratorPairs2 = secrets2.ZipForceEqualLength(generators2);
			foreach (var (secret, generator) in secretGeneratorPairs2)
			{
				publicPoint2 += secret * generator;
			}

			var proof = Prover.CreateAndProof(new[]
			{
				new KnowledgeOfRepresentationParameters(secretGeneratorPairs, publicPoint),
				new KnowledgeOfRepresentationParameters(secretGeneratorPairs2, publicPoint2)
			});
			Assert.True(Verifier.Verify(proof, new[] { new Statement(publicPoint, generators), new Statement(publicPoint2, generators2) }));
		}
	}
}
