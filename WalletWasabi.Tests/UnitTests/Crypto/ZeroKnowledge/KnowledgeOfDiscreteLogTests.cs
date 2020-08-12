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
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;

			var publicPoint = point1;
			var nonce = Generators.G;
			var challenge = Challenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0x63CB4683UL, 0xE74DC9A9UL, 0x346534D4UL, 0x247AF71FUL, 0xB49BF19DUL, 0x127658B1UL, 0xE80264F6UL, 0xAE87D410UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			challenge = Challenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0xE2D33BB7UL, 0xA303E090UL, 0x61094B47UL, 0xE689400BUL, 0xE4B97858UL, 0xC92E9B00UL, 0xFE5D6531UL, 0x8F14CF73UL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			challenge = Challenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0xCBF4E2E0UL, 0xCB4E26D0UL, 0xAD167C64UL, 0x3083BA72UL, 0xD74AB657UL, 0xA1F544D6UL, 0x732BFC65UL, 0xF11CEAABUL }", challenge.ToC(""));
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
