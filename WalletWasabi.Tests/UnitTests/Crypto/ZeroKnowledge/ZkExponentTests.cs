using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class ZkExponentTests
	{
		public static readonly Scalar LargestScalarOverflow = new Scalar(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(5)]
		[InlineData(7)]
		[InlineData(short.MaxValue)]
		[InlineData(int.MaxValue)]
		[InlineData(uint.MaxValue)]
		public void VerifySimpleProof(uint scalarSeed)
		{
			var exponent = new Scalar(scalarSeed);
			var gen = new Scalar(2) * GroupElement.G;
			var p = exponent * gen;
			var proof = ZkProver.CreateProof(exponent, p, gen);
			Assert.True(ZkVerifier.Verify(proof, p, gen));
		}

		[Fact]
		public void VerifiesFalse()
		{
			// Even if the challenge is correct, because the public input in the hash is right,
			// if the final response is not valid wrt the verification equation,
			// the verifier should still reject.
			var secret = new Scalar(7);
			var generator = GroupElement.G;
			var publicPoint = secret * generator;

			Scalar randomScalar = new Scalar(14);
			var randomPoint = randomScalar * generator;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);

			var response = randomScalar + (secret + Scalar.One) * challenge;
			var proof = new ZkExponentProof(randomPoint, response);
			Assert.False(ZkVerifier.Verify(proof, publicPoint, generator));

			// Other false verification tests.
			var point1 = new Scalar(3) * GroupElement.G;
			var point2 = new Scalar(7) * GroupElement.G;
			var scalar = new Scalar(11);
			var gen = GroupElement.G;

			proof = new ZkExponentProof(point1, scalar);
			Assert.False(ZkVerifier.Verify(proof, point2, gen));
		}

		[Fact]
		public void VerifyLargeScalar()
		{
			uint val = int.MaxValue;
			var gen = new Scalar(4) * GroupElement.G;
			var exponent = new Scalar(val, val, val, val, val, val, val, val);
			var p = exponent * gen;
			var proof = ZkProver.CreateProof(exponent, p, gen);
			Assert.True(ZkVerifier.Verify(proof, p, gen));

			exponent = EC.N + (new Scalar(1)).Negate();
			p = exponent * gen;
			proof = ZkProver.CreateProof(exponent, p, gen);
			Assert.True(ZkVerifier.Verify(proof, p, gen));

			exponent = EC.NC;
			p = exponent * gen;
			proof = ZkProver.CreateProof(exponent, p, gen);
			Assert.True(ZkVerifier.Verify(proof, p, gen));
			exponent = EC.NC + new Scalar(1);
			p = exponent * gen;
			proof = ZkProver.CreateProof(exponent, p, gen);
			Assert.True(ZkVerifier.Verify(proof, p, gen));
			exponent = EC.NC + (new Scalar(1)).Negate();
			p = exponent * gen;
			proof = ZkProver.CreateProof(exponent, p, gen);
			Assert.True(ZkVerifier.Verify(proof, p, gen));
		}

		[Fact]
		public void BuildChallenge()
		{
			var point1 = new Scalar(3) * GroupElement.G;
			var point2 = new Scalar(7) * GroupElement.G;

			var publicPoint = point1;
			var randomPoint = GroupElement.G;
			var challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x0F850F8CUL, 0x9D74683AUL, 0xDD03779BUL, 0xFD58F09BUL, 0xE148D87AUL, 0x3477A63FUL, 0xBE5906D3UL, 0x35E5A382UL }", challenge.ToC(""));

			publicPoint = GroupElement.G;
			randomPoint = point2;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0x69823107UL, 0xDA1CE96BUL, 0xBA00C8E7UL, 0x8A031437UL, 0x4D0BC9ADUL, 0x790E6FD8UL, 0x6C2EF5E6UL, 0x8F476E3FUL }", challenge.ToC(""));

			publicPoint = point1;
			randomPoint = point2;
			challenge = ZkChallenge.Build(publicPoint, randomPoint);
			Assert.Equal("secp256k1_scalar  = { 0xC5AA9243UL, 0x074DDA7CUL, 0xE8FFD6CAUL, 0xF3613B9EUL, 0x542CBD09UL, 0xF4191712UL, 0x045BD716UL, 0xECCC6626UL }", challenge.ToC(""));
		}

		[Fact]
		public void ChallengeThrows()
		{
			// Demonstrate when it shouldn't throw.
			ZkChallenge.Build(GroupElement.G, GroupElement.Ga);

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => ZkChallenge.Build(GroupElement.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkChallenge.Build(GroupElement.Infinity, GroupElement.Ga));
			Assert.ThrowsAny<ArgumentException>(() => ZkChallenge.Build(GroupElement.Infinity, GroupElement.Infinity));

			// Public and random points cannot be the same.
			Assert.ThrowsAny<InvalidOperationException>(() => ZkChallenge.Build(GroupElement.G, GroupElement.G));
		}

		[Fact]
		public void ExponentProofThrows()
		{
			// Demonstrate when it shouldn't throw.
			new ZkExponentProof(GroupElement.G, Scalar.One);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new ZkExponentProof(GroupElement.G, Scalar.Zero));
			Assert.ThrowsAny<ArgumentException>(() => new ZkExponentProof(GroupElement.Infinity, Scalar.One));
			Assert.ThrowsAny<ArgumentException>(() => new ZkExponentProof(GroupElement.Infinity, Scalar.Zero));
		}

		[Fact]
		public void ProverThrows()
		{
			var two = new Scalar(2);

			// Demonstrate when it shouldn't throw.
			ZkProver.CreateProof(two, two * GroupElement.G, GroupElement.G);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, two * GroupElement.G, GroupElement.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(two, GroupElement.Infinity, GroupElement.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(two, two * GroupElement.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, GroupElement.Infinity, GroupElement.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, two * GroupElement.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(two, GroupElement.Infinity, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, GroupElement.Infinity, GroupElement.Infinity));

			// Public point must be generator * exponent.
			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(two, GroupElement.G, GroupElement.G));
			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(two, new Scalar(3) * GroupElement.G, GroupElement.G));
			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(two, Scalar.One * GroupElement.G, GroupElement.G));

			// Exponent cannot overflow.
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(EC.N, EC.N * GroupElement.G, GroupElement.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(LargestScalarOverflow, LargestScalarOverflow * GroupElement.G, GroupElement.G));
		}

		[Fact]
		public void VerifierThrows()
		{
			var proof = new ZkExponentProof(GroupElement.G, Scalar.One);

			// Demonstrate when it shouldn't throw.
			ZkVerifier.Verify(proof, GroupElement.Ga, GroupElement.Gg);

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => ZkVerifier.Verify(proof, GroupElement.Infinity, GroupElement.Gg));
			Assert.ThrowsAny<ArgumentException>(() => ZkVerifier.Verify(proof, GroupElement.Ga, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkVerifier.Verify(proof, GroupElement.Infinity, GroupElement.Infinity));

			// Public point should not be equal to the random point of the proof.
			Assert.ThrowsAny<InvalidOperationException>(() => ZkVerifier.Verify(proof, GroupElement.G, GroupElement.Ga));
		}
	}
}
