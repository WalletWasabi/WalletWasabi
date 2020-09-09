using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			var statement = new Statement(publicPoint, generator);
			var knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
			var proof = Prover.CreateProof(knowledgeParams);
			Assert.True(Verifier.Verify(proof, statement));
		}

		[Fact]
		public void End2EndVerification()
		{
			foreach (var secret in CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero))
			{
				var generator = Generators.G;
				var publicPoint = secret * generator;
				var statement = new Statement(publicPoint, generator);
				var knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
				var proof = Prover.CreateProof(knowledgeParams);
				Assert.True(Verifier.Verify(proof, statement));
			}
		}

		[Fact]
		public void End2EndVerificationLargeScalar()
		{
			uint val = int.MaxValue;
			var gen = new Scalar(4) * Generators.G;
			var secret = new Scalar(val, val, val, val, val, val, val, val);
			var p = secret * gen;
			var statement = new Statement(p, gen);
			var knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
			var proof = Prover.CreateProof(knowledgeParams);
			Assert.True(Verifier.Verify(proof, statement));

			secret = EC.N + (new Scalar(1)).Negate();
			p = secret * gen;
			statement = new Statement(p, gen);
			knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
			proof = Prover.CreateProof(knowledgeParams);
			Assert.True(Verifier.Verify(proof, statement));

			secret = EC.NC;
			p = secret * gen;
			statement = new Statement(p, gen);
			knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
			proof = Prover.CreateProof(knowledgeParams);
			Assert.True(Verifier.Verify(proof, statement));
			secret = EC.NC + new Scalar(1);
			p = secret * gen;
			statement = new Statement(p, gen);
			knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
			proof = Prover.CreateProof(knowledgeParams);
			Assert.True(Verifier.Verify(proof, statement));
			secret = EC.NC + (new Scalar(1)).Negate();
			p = secret * gen;
			statement = new Statement(p, gen);
			knowledgeParams = new KnowledgeOfDlogParams(secret, statement);
			proof = Prover.CreateProof(knowledgeParams);
			Assert.True(Verifier.Verify(proof, statement));
		}

		[Fact]
		public void BuildChallenge()
		{
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var generator = Generators.Ga;

			var publicPoint = point1;
			var nonce = Generators.G;
			var statement = new Statement(publicPoint, generator);
			var challenge = Challenge.Build(nonce, statement);
			Assert.Equal("secp256k1_scalar  = { 0x82CF8030UL, 0x5B5AED14UL, 0x91C6BBFEUL, 0x798A4258UL, 0x1971D3ABUL, 0x0BD8C47BUL, 0x791A5ABDUL, 0xC0E7C23EUL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			statement = new Statement(publicPoint, generator);
			challenge = Challenge.Build(nonce, statement);
			Assert.Equal("secp256k1_scalar  = { 0x3E6A930DUL, 0x04F4CB3FUL, 0xEAAA2D36UL, 0xF303D033UL, 0x8EB4012AUL, 0x2B975875UL, 0x8CFDC425UL, 0x249C3446UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			statement = new Statement(publicPoint, generator);
			challenge = Challenge.Build(nonce, statement);
			Assert.Equal("secp256k1_scalar  = { 0x26BA45F5UL, 0x062AD4B6UL, 0x6C642CF4UL, 0x01677E65UL, 0x68017D1CUL, 0x19B47BBCUL, 0x6AF10CD6UL, 0x8273EA54UL }", challenge.ToC(""));
		}

		[Fact]
		public void KnowledgeOfDlogThrows()
		{
			// Demonstrate when it shouldn't throw.
			new KnowledgeOfDlog(Generators.G, Scalar.One);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDlog(Generators.G, Scalar.Zero));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDlog(GroupElement.Infinity, Scalar.One));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDlog(GroupElement.Infinity, Scalar.Zero));
		}

		[Fact]
		public void KnowledgeOfDlogParamsThrows()
		{
			var two = new Scalar(2);

			// Demonstrate when it shouldn't throw.
			new KnowledgeOfDlogParams(two, new Statement(two * Generators.G, Generators.G));

			// Zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDlogParams(Scalar.Zero, new Statement(Generators.G, Generators.G)));

			// Public point must be generator * secret.
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfDlogParams(two, new Statement(Generators.G, Generators.G)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfDlogParams(two, new Statement(new Scalar(3) * Generators.G, Generators.G)));
			Assert.ThrowsAny<InvalidOperationException>(() => new KnowledgeOfDlogParams(two, new Statement(Scalar.One * Generators.G, Generators.G)));

			// Secret cannot overflow.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDlogParams(EC.N, new Statement(EC.N * Generators.G, Generators.G)));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDlogParams(CryptoHelpers.ScalarLargestOverflow, new Statement(CryptoHelpers.ScalarLargestOverflow * Generators.G, Generators.G)));
		}
	}
}
