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
				.Commit(new Statement(publicPoint, generator))
				.Commit(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0x901749C2UL, 0x4814BFCBUL, 0x71A4FC57UL, 0xA6D99D4BUL, 0x04D291B6UL, 0x6DFCC73EUL, 0x331F5BB8UL, 0xD48EA8B6UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			challenge = new Transcript()
				.Commit(new Statement(publicPoint, generator))
				.Commit(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0x1E6F79A0UL, 0xB9F46D08UL, 0xE1F09E92UL, 0xBDF27DE1UL, 0x49A4899BUL, 0x8E332803UL, 0x5C3BF87BUL, 0x6C6819BDUL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			challenge = new Transcript()
				.Commit(new Statement(publicPoint, generator))
				.Commit(nonce)
				.GenerateChallenge()
				.challenge;
			Assert.Equal("secp256k1_scalar  = { 0xCA19952DUL, 0x9EA4EB0DUL, 0xE1BB4112UL, 0x92A4D095UL, 0xEC08ABC2UL, 0x14E5781DUL, 0xDD64707BUL, 0xC258A3A5UL }", challenge.ToC(""));
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
