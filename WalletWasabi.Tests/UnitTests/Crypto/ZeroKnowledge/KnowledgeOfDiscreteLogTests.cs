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
	public class KnowledgeOfDiscreteLogTests
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
			var proof = Prover.CreateProof(secret, statement);
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
				var proof = Prover.CreateProof(secret, statement);
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
			var proof = Prover.CreateProof(secret, statement);
			Assert.True(Verifier.Verify(proof, statement));

			secret = EC.N + (new Scalar(1)).Negate();
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = Prover.CreateProof(secret, statement);
			Assert.True(Verifier.Verify(proof, statement));

			secret = EC.NC;
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = Prover.CreateProof(secret, statement);
			Assert.True(Verifier.Verify(proof, statement));
			secret = EC.NC + new Scalar(1);
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = Prover.CreateProof(secret, statement);
			Assert.True(Verifier.Verify(proof, statement));
			secret = EC.NC + (new Scalar(1)).Negate();
			p = secret * gen;
			statement = new Statement(p, gen);
			proof = Prover.CreateProof(secret, statement);
			Assert.True(Verifier.Verify(proof, statement));
		}

		[Fact]
		public void BuildChallenge()
		{
			// Mostly superseded by transcript tests, can be removed apart from test vectors
			var mockRandom = new MockRandom();
			mockRandom.GetBytesResults.Add(new byte[32]);
			mockRandom.GetBytesResults.Add(new byte[32]);
			mockRandom.GetBytesResults.Add(new byte[32]);

			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var generator = Generators.Ga;

			var publicPoint = point1;
			var nonce = Generators.G;
			var transcript = new Transcript();
			transcript.Statement(new Statement(publicPoint, generator));
			Scalar randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			var challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0x76DE2CD1UL, 0x6B2058F6UL, 0x1AFCD67BUL, 0x6FA6F6D9UL, 0xE3616642UL, 0x2B7C7937UL, 0x2CEAC4FAUL, 0xADBD6816UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			transcript = new Transcript();
			transcript.Statement(new Statement(publicPoint, generator));
			randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0xD5C21CC5UL, 0xDF25A2C4UL, 0x138537BEUL, 0xCAF4DB7FUL, 0x4A74E2F5UL, 0x4E38C5FEUL, 0x8DF3E37DUL, 0xC9009D2CUL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			transcript = new Transcript();
			transcript.Statement(new Statement(publicPoint, generator));
			randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0xBF46571BUL, 0x046ACBCBUL, 0xC374C02BUL, 0x23517B5AUL, 0x71CD44C8UL, 0xD15376F4UL, 0xB7785149UL, 0xCE2E541EUL }", challenge.ToC(""));
		}

		[Fact]
		public void Throws()
		{
			// Demonstrate when it shouldn't throw.
			new KnowledgeOfDiscreteLog(Generators.G, Scalar.One);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDiscreteLog(Generators.G, Scalar.Zero));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDiscreteLog(GroupElement.Infinity, Scalar.One));
			Assert.ThrowsAny<ArgumentException>(() => new KnowledgeOfDiscreteLog(GroupElement.Infinity, Scalar.Zero));
		}
	}
}
