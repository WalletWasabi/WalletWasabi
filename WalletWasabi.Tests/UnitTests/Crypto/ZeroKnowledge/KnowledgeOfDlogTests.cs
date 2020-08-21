using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.Transcripting;
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
			// Mostly superseded by transcript tests, can be removed apart from test vectors.
			var mockRandom = new MockRandom();
			mockRandom.GetBytesResults.Add(new byte[32]);
			mockRandom.GetBytesResults.Add(new byte[32]);
			mockRandom.GetBytesResults.Add(new byte[32]);

			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var generator = Generators.Ga;

			var publicPoint = point1;
			var nonce = Generators.G;
			var challenge = new Transcript()
				.CommitToStatement(new Statement(publicPoint, generator))
				.NonceCommitment(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0x366EA32EUL, 0xF17B8A20UL, 0xE8D4C22DUL, 0x1A601DAFUL, 0xF240A50CUL, 0x12CDF005UL, 0xF04FCEFCUL, 0xFC4B70D3UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			challenge = new Transcript()
				.CommitToStatement(new Statement(publicPoint, generator))
				.NonceCommitment(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0xB8A1ADADUL, 0xD33B6BFEUL, 0x9F3353C3UL, 0x3BFE6AEEUL, 0xDE0769C2UL, 0x36DB9527UL, 0x7954F334UL, 0x591EBDA6UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			challenge = new Transcript()
				.CommitToStatement(new Statement(publicPoint, generator))
				.NonceCommitment(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0xBF5A84E4UL, 0xE1E6D5EEUL, 0xD7473C55UL, 0x536B34A7UL, 0xC6435F50UL, 0xE5D7FA77UL, 0xE8E3C8B5UL, 0x84DC7BB2UL }", challenge.ToC(""));
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
