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
			Assert.Equal("secp256k1_scalar  = { 0x37D36429UL, 0x8EE0695FUL, 0x2F64F636UL, 0x15A0C0EDUL, 0x42181802UL, 0x14AC3251UL, 0xA42741DAUL, 0x83A2620FUL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			challenge = new Transcript()
				.CommitToStatement(new Statement(publicPoint, generator))
				.NonceCommitment(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0x178CFC8EUL, 0xFF981131UL, 0x4AA2BBB3UL, 0xF54A92C9UL, 0x26771AC7UL, 0x350C98E2UL, 0x85018A7CUL, 0x6AC6F364UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			challenge = new Transcript()
				.CommitToStatement(new Statement(publicPoint, generator))
				.NonceCommitment(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0xBE4B4367UL, 0x71241F2CUL, 0x6AD2D560UL, 0x7C9D0302UL, 0xD88214D9UL, 0x26B83492UL, 0xC0FE35A7UL, 0x8BAAF659UL }", challenge.ToC(""));
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
