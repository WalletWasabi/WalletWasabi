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
			Assert.Equal("secp256k1_scalar  = { 0x0424C37EUL, 0x2B276403UL, 0xF63F4D09UL, 0xBD22FB8EUL, 0xFBABE75CUL, 0x7EFD3E1DUL, 0x3413E1B5UL, 0xC717EFB7UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			transcript = new Transcript();
			transcript.Statement(new Statement(publicPoint, generator));
			randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0x846DF9D6UL, 0xED86ED32UL, 0x1F014B12UL, 0x16F2670CUL, 0x567C9019UL, 0xBE1804DBUL, 0x86E81D51UL, 0x3F8ECF84UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			transcript = new Transcript();
			transcript.Statement(new Statement(publicPoint, generator));
			randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0xC8162314UL, 0x1C11F776UL, 0xC465D40CUL, 0xBF6B870DUL, 0x16C3DFBFUL, 0xCD4F30D8UL, 0x34641937UL, 0x5DEB799EUL }", challenge.ToC(""));
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
