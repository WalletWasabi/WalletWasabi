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
			var proof = Prover.CreateProof(secret, publicPoint, generator);
			Assert.True(Verifier.Verify(proof, publicPoint, generator));
		}

		[Fact]
		public void End2EndVerification()
		{
			foreach (var secret in CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero))
			{
				var generator = Generators.G;
				var publicPoint = secret * generator;
				var proof = Prover.CreateProof(secret, publicPoint, generator);
				Assert.True(Verifier.Verify(proof, publicPoint, generator));
			}
		}

		[Fact]
		public void End2EndVerificationLargeScalar()
		{
			uint val = int.MaxValue;
			var gen = new Scalar(4) * Generators.G;
			var secret = new Scalar(val, val, val, val, val, val, val, val);
			var p = secret * gen;
			var proof = Prover.CreateProof(secret, p, gen);
			Assert.True(Verifier.Verify(proof, p, gen));

			secret = EC.N + (new Scalar(1)).Negate();
			p = secret * gen;
			proof = Prover.CreateProof(secret, p, gen);
			Assert.True(Verifier.Verify(proof, p, gen));

			secret = EC.NC;
			p = secret * gen;
			proof = Prover.CreateProof(secret, p, gen);
			Assert.True(Verifier.Verify(proof, p, gen));
			secret = EC.NC + new Scalar(1);
			p = secret * gen;
			proof = Prover.CreateProof(secret, p, gen);
			Assert.True(Verifier.Verify(proof, p, gen));
			secret = EC.NC + (new Scalar(1)).Negate();
			p = secret * gen;
			proof = Prover.CreateProof(secret, p, gen);
			Assert.True(Verifier.Verify(proof, p, gen));
		}

		[Fact]
		public void BuildChallenge()
		{
			// Mostly superseded by transcript tests, can be removed apart from test vectors

			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;

			var mockRandom = new MockRandom();
			mockRandom.GetBytesResults.Add(new byte[32]);
			mockRandom.GetBytesResults.Add(new byte[32]);
			mockRandom.GetBytesResults.Add(new byte[32]);

			var publicPoint = point1;
			var nonce = Generators.G;
			var tag = Encoding.UTF8.GetBytes("");
			var transcript = new Transcript();
			transcript.Statement(tag, publicPoint, Generators.G);
			Scalar randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			var challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0x2A5B1BC7UL, 0xEBF35A1AUL, 0xB996152FUL, 0x3F33139FUL, 0x001C6628UL, 0x976CD8C4UL, 0xC3B77988UL, 0xC692E569UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			transcript = new Transcript();
			transcript.Statement(tag, publicPoint, Generators.G);
			randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0x5C135111UL, 0x7C4F01C9UL, 0x56562BCDUL, 0xFCFD7771UL, 0xB1E7BA66UL, 0xF4260CCEUL, 0x12E3DF36UL, 0x23264818UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			transcript = new Transcript();
			transcript.Statement(tag, publicPoint, Generators.G);
			randomScalar = transcript.GenerateNonce(Scalar.One, mockRandom);
			transcript.NonceCommitment(nonce);
			challenge = transcript.GenerateChallenge();
			Assert.Equal("secp256k1_scalar  = { 0x935F76BAUL, 0x9BD463EAUL, 0x3930D47BUL, 0x2911ECEEUL, 0xD6C2CCEDUL, 0x725F12DEUL, 0xADEDE8DAUL, 0xADC7FB8FUL }", challenge.ToC(""));
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
