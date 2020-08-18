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
	public class KnowledgeOfRepresentationTests
	{
		[Theory]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(3, 5)]
		[InlineData(5, 7)]
		[InlineData(7, 11)]
		[InlineData(short.MaxValue, uint.MaxValue)]
		[InlineData(int.MaxValue, uint.MaxValue)]
		[InlineData(uint.MaxValue, uint.MaxValue)]
		public void End2EndVerificationSimple(uint scalarSeed1, uint scalarSeed2)
		{
			var secrets = new[] { new Scalar(scalarSeed1), new Scalar(scalarSeed2) };
			var generators = new[] { Generators.G, Generators.Ga };
			var publicPoint = GroupElement.Infinity;
			var secretGeneratorPairs = secrets.ZipForceEqualLength(generators);
			foreach (var (secret, generator) in secretGeneratorPairs)
			{
				publicPoint += secret * generator;
			}
			var proof = Prover.CreateProof(new KnowledgeOfRepresentationParameters(secretGeneratorPairs, publicPoint));
			Assert.True(Verifier.Verify(proof, publicPoint, generators));
		}

		[Fact]
		public void End2EndVerification()
		{
			var goodScalars = CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero);
			foreach (var secret1 in goodScalars)
			{
				foreach (var secret2 in goodScalars.Where(x => x != secret1))
				{
					var secrets = new[] { secret1, secret2 };
					var generators = new[] { Generators.G, Generators.Ga };
					var publicPoint = GroupElement.Infinity;
					var secretGeneratorPairs = secrets.ZipForceEqualLength(generators);
					foreach (var (secret, generator) in secretGeneratorPairs)
					{
						publicPoint += secret * generator;
					}
					var proof = Prover.CreateProof(new KnowledgeOfRepresentationParameters(secretGeneratorPairs, publicPoint));
					Assert.True(Verifier.Verify(proof, publicPoint, generators));
				}
			}
		}

		[Fact]
		public void Throws()
		{
			// Demonstrate when it shouldn't throw.
			new KnowledgeOfRepresentation(Generators.G, CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepresentation(Generators.G, Scalar.Zero, CryptoHelpers.ScalarTwo));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepresentation(Generators.G, CryptoHelpers.ScalarOne, Scalar.Zero));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepresentation(GroupElement.Infinity, CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo));
		}
	}
}
