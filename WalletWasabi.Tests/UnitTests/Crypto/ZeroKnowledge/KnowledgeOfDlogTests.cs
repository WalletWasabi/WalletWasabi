using NBitcoin.Secp256k1;
using System;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using Xunit;
using LR = WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;

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
			var statement = new LR.Statement(publicPoint, generator);
			var random = new MockRandom();
			random.GetBytesResults.Add(new byte[32]);
			var proof = ProofSystem.Prove(statement, secret, random);
			Assert.True(ProofSystem.Verify(statement, proof));
		}

		[Fact]
		public void End2EndVerification()
		{
			foreach (var secret in CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero))
			{
				var generator = Generators.G;
				var publicPoint = secret * generator;
				var statement = new LR.Statement(publicPoint, Generators.G);
				var proof = ProofSystem.Prove(statement, secret, new SecureRandom());
				Assert.True(ProofSystem.Verify(statement, proof));
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
			var statement = new LR.Statement(p, gen);
			var proof = ProofSystem.Prove(statement, secret, random);
			Assert.True(ProofSystem.Verify(statement, proof));

			secret = EC.N + Scalar.One.Negate();
			p = secret * gen;
			statement = new LR.Statement(p, gen);
			proof = ProofSystem.Prove(statement, secret, random);
			Assert.True(ProofSystem.Verify(statement, proof));

			secret = EC.NC;
			p = secret * gen;
			statement = new LR.Statement(p, gen);
			proof = ProofSystem.Prove(statement, secret, random);
			Assert.True(ProofSystem.Verify(statement, proof));

			secret = EC.NC + Scalar.One;
			p = secret * gen;
			statement = new LR.Statement(p, gen);
			proof = ProofSystem.Prove(statement, secret, random);
			Assert.True(ProofSystem.Verify(statement, proof));

			secret = EC.NC + Scalar.One.Negate();
			p = secret * gen;
			statement = new LR.Statement(p, gen);
			proof = ProofSystem.Prove(statement, secret, random);
			Assert.True(ProofSystem.Verify(statement, proof));
		}
	}
}
