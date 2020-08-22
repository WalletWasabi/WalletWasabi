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
	public class KnowledgeOfRepTests
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
			foreach (var (secret, generator) in secrets.ZipForceEqualLength(generators))
			{
				publicPoint += secret * generator;
			}

			var statement = new Statement(publicPoint, generators);
			var knowledge = new KnowledgeOfRepParams(secrets, statement);
			var proof = Prover.CreateProof(knowledge);
			Assert.True(Verifier.Verify(proof, statement));
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
					foreach (var (secret, generator) in secrets.ZipForceEqualLength(generators))
					{
						publicPoint += secret * generator;
					}
					Statement statement = new Statement(publicPoint, generators);
					var knowledge = new KnowledgeOfRepParams(secrets, statement);
					var proof = Prover.CreateProof(knowledge);
					Assert.True(Verifier.Verify(proof, statement));
				}
			}
		}

		[Fact]
		public void KnowledgeOfRepThrows()
		{
			// Demonstrate when it shouldn't throw.
			new KnowledgeOfRep(Generators.G, CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRep(Generators.G, Scalar.Zero, CryptoHelpers.ScalarTwo));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRep(Generators.G, CryptoHelpers.ScalarOne, Scalar.Zero));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRep(GroupElement.Infinity, CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo));
		}

		[Fact]
		public void KnowledgeOfRepParamsThrows()
		{
			var two = new Scalar(2);
			var three = new Scalar(3);

			// Demonstrate when it shouldn't throw.
			new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga));

			// Zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepParams(new[] { Scalar.Zero, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepParams(new[] { two, Scalar.Zero }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));

			// Overflow cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepParams(new[] { EC.N, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepParams(new[] { two, EC.N }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));

			// Public point must be sum(generator * secret).
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(three * Generators.Ga, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.Ga, Generators.G)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Gg)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + Generators.Ga, Generators.G, Generators.Ga)));

			// Generators and secrets cannot be empty.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepParams(Array.Empty<Scalar>(), new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + three * Generators.Ga)));

			// Generators and secrets must be equal.
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two }, new Statement(two * Generators.G, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three, new Scalar(4) }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfRepParams(new[] { two, three }, new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga, Generators.Gg)));
		}
	}
}
