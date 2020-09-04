using NBitcoin.Secp256k1;
using System;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class KnowledgeOfDLogTests
	{
		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(5)]
		[InlineData(7)]
		[InlineData(short.MaxValue)]
		[InlineData(int.MaxValue)]
		[InlineData(uint.MaxValue)]
		public void End2EndVerificationSimple(uint scalarSeed)
		{
			var secret = new Scalar(scalarSeed);
			var generator = Generators.G;
			var publicPoint = secret * generator;
			var statement = Proofs.DiscreteLog(publicPoint, generator);
			var random = new MockRandom();
			random.GetBytesResults.Add(new byte[32]);
			var proof = Proofs.CreateProof(statement, secret, random);
			Assert.True(Proofs.CheckProof(statement, proof));
		}

		[Fact]
		public void End2EndVerification()
		{
			foreach (var secret in CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero))
			{
				var generator = Generators.G;
				var publicPoint = secret * generator;
				var statement = Proofs.DiscreteLog(publicPoint, Generators.G);
				var proof = Proofs.CreateProof(statement, secret, new SecureRandom());
				Assert.True(Proofs.CheckProof(statement, proof));
			}
		}

		[Fact]
		public void End2EndVerificationLargeScalar()
		{
			var random = new SecureRandom();
			uint val = int.MaxValue;
			var gen = new Scalar(4) * Generators.G;

			var secret = new Scalar(val, val, val, val, val, val, val, val);
			var p = secret * gen;
			var statement = Proofs.DiscreteLog(p, gen);
			var proof = Proofs.CreateProof(statement, secret, random);
			Assert.True(Proofs.CheckProof(statement, proof));

			secret = EC.N + Scalar.One.Negate();
			p = secret * gen;
			statement = Proofs.DiscreteLog(p, gen);
			proof = Proofs.CreateProof(statement, secret, random);
			Assert.True(Proofs.CheckProof(statement, proof));

			secret = EC.NC;
			p = secret * gen;
			statement = Proofs.DiscreteLog(p, gen);
			proof = Proofs.CreateProof(statement, secret, random);
			Assert.True(Proofs.CheckProof(statement, proof));

			secret = EC.NC + Scalar.One;
			p = secret * gen;
			statement = Proofs.DiscreteLog(p, gen);
			proof = Proofs.CreateProof(statement, secret, random);
			Assert.True(Proofs.CheckProof(statement, proof));

			secret = EC.NC + Scalar.One.Negate();
			p = secret * gen;
			statement = Proofs.DiscreteLog(p, gen);
			proof = Proofs.CreateProof(statement, secret, random);
			Assert.True(Proofs.CheckProof(statement, proof));
		}

		[Fact]
		public void KnowledgeOfDlogParamsThrows()
		{
			var two = new Scalar(2);

			// Demonstrate when it shouldn't throw.
			// Proofs.DiscreteLog(two, Generators.G);

			// Zero cannot pass through.
			// Assert.ThrowsAny<ArgumentException>(() => Proofs.DiscreteLog(Scalar.Zero, Generators.G));

			// TODO
			// // Secret cannot overflow.
			// Assert.ThrowsAny<ArgumentException>(() => Proofs.DiscreteLog(EC.N, Generators.G));
			// Assert.ThrowsAny<ArgumentException>(() => Proofs.DiscreteLog(CryptoHelpers.ScalarLargestOverflow, Generators.G));
		}
	}
}
