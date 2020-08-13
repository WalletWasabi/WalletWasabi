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
			Assert.Equal("secp256k1_scalar  = { 0x0F850F8CUL, 0x9D74683AUL, 0xDD03779BUL, 0xFD58F09BUL, 0xE148D87AUL, 0x3477A63FUL, 0xBE5906D3UL, 0x35E5A382UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			challenge = Challenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0x69823107UL, 0xDA1CE96BUL, 0xBA00C8E7UL, 0x8A031437UL, 0x4D0BC9ADUL, 0x790E6FD8UL, 0x6C2EF5E6UL, 0x8F476E3FUL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			challenge = Challenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0xC5AA9243UL, 0x074DDA7CUL, 0xE8FFD6CAUL, 0xF3613B9EUL, 0x542CBD09UL, 0xF4191712UL, 0x045BD716UL, 0xECCC6626UL }", challenge.ToC(""));
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
