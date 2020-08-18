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
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var generator = Generators.Ga;

			var publicPoint = point1;
			var nonce = Generators.G;
			var statement = new Statement(publicPoint, generator);
			var challenge = Challenge.Build(nonce, statement);
			Assert.Equal("secp256k1_scalar  = { 0x8626D370UL, 0x6D18AF98UL, 0xAE71F87DUL, 0x5008741FUL, 0x43515E2BUL, 0x666194D8UL, 0x97CCA524UL, 0x09E82A30UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			statement = new Statement(publicPoint, generator);
			challenge = Challenge.Build(nonce, statement);
			Assert.Equal("secp256k1_scalar  = { 0x72CF8E90UL, 0x518E892FUL, 0xA7046699UL, 0x0C21C88FUL, 0xEE3DC26EUL, 0x83F833FBUL, 0x0B21A692UL, 0x404C3D01UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			statement = new Statement(publicPoint, generator);
			challenge = Challenge.Build(nonce, statement);
			Assert.Equal("secp256k1_scalar  = { 0x333575CAUL, 0x7D400596UL, 0x4C12B718UL, 0xD64F97BBUL, 0x5EBB4EB7UL, 0x2D5DFD8EUL, 0xFB2FE4DCUL, 0x49FD37B6UL }", challenge.ToC(""));
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
