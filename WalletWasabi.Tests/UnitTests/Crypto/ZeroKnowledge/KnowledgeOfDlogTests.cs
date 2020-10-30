using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
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
			var statement = new Statement(publicPoint, generator);
			var random = new MockRandom();
			random.GetBytesResults.Add(new byte[32]);
			var proof = ProofSystemHelpers.Prove(statement, secret, random);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));
		}

		[Fact]
		public void End2EndVerification()
		{
			foreach (var secret in CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero))
			{
				var generator = Generators.G;
				var publicPoint = secret * generator;
				var statement = new Statement(publicPoint, Generators.G);
				var proof = ProofSystemHelpers.Prove(statement, secret, new SecureRandom());
				Assert.True(ProofSystemHelpers.Verify(statement, proof));
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
			var statement = new Statement(p, gen);
			var proof = ProofSystemHelpers.Prove(statement, secret, random);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));

			secret = EC.N + Scalar.One.Negate();
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = ProofSystemHelpers.Prove(statement, secret, random);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));

			secret = EC.NC;
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = ProofSystemHelpers.Prove(statement, secret, random);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));

			secret = EC.NC + Scalar.One;
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = ProofSystemHelpers.Prove(statement, secret, random);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));

			secret = EC.NC + Scalar.One.Negate();
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = ProofSystemHelpers.Prove(statement, secret, random);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));
		}
	}
}
