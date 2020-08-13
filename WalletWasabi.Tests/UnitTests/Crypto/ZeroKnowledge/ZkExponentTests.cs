using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class ZkExponentTests
	{
		public static readonly Scalar ScalarLargestOverflow = new Scalar(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
		public static readonly Scalar ScalarN = EC.N;
		public static readonly Scalar ScalarEcnPlusOne = EC.N + Scalar.One;
		public static readonly Scalar ScalarEcnMinusOne = EC.N + Scalar.One.Negate(); // Largest non-overflown scalar.
		public static readonly Scalar ScalarLarge = new Scalar(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
		public static readonly Scalar ScalarZero = Scalar.Zero;
		public static readonly Scalar ScalarOne = Scalar.One;
		public static readonly Scalar ScalarTwo = new Scalar(2);
		public static readonly Scalar ScalarThree = new Scalar(3);
		public static readonly Scalar ScalarEcnc = EC.NC;

		public static IEnumerable<Scalar> GetScalars(Func<Scalar, bool> predicate)
		{
			var scalars = new List<Scalar>
			{
				ScalarLargestOverflow,
				ScalarN,
				ScalarEcnPlusOne,
				ScalarEcnMinusOne,
				ScalarLarge,
				ScalarZero,
				ScalarOne,
				ScalarTwo,
				ScalarThree,
				ScalarEcnc
			};

			return scalars.Where(predicate);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(5)]
		[InlineData(7)]
		[InlineData(short.MaxValue)]
		[InlineData(int.MaxValue)]
		[InlineData(uint.MaxValue)]
		public void End2EndVerifiesSimpleProof(uint scalarSeed)
		{
			var exponent = new Scalar(scalarSeed);
			var generator = Generators.G;
			var publicPoint = exponent * generator;
			var proof = ZkProver.CreateProof(exponent, publicPoint, generator);
			Assert.True(ZkVerifier.Verify(proof, publicPoint, generator));
		}

		[Fact]
		public void End2EndVerifiesExponents()
		{
			foreach (var exponent in GetScalars(x => !x.IsOverflow && !x.IsZero))
			{
				var generator = Generators.G;
				var publicPoint = exponent * generator;
				var proof = ZkProver.CreateProof(exponent, publicPoint, generator);
				Assert.True(ZkVerifier.Verify(proof, publicPoint, generator));
			}
		}

		[Fact]
		public void VerifiesToFalse()
		{
			// Even if the challenge is correct, because the public input in the hash is right,
			// if the final response is not valid wrt the verification equation,
			// the verifier should still reject.
			var secret = new Scalar(7);
			var generator = Generators.G;
			var publicPoint = secret * generator;

			Scalar randomScalar = new Scalar(14);
			var nonce = randomScalar * generator;
			var challenge = ZkChallenge.Build(publicPoint, nonce);

			var response = randomScalar + (secret + Scalar.One) * challenge;
			var proof = new ZkKnowledgeOfExponent(nonce, response);
			Assert.False(ZkVerifier.Verify(proof, publicPoint, generator));

			// Other false verification tests.
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var scalar = new Scalar(11);
			var gen = Generators.G;

			proof = new ZkKnowledgeOfExponent(point1, scalar);
			Assert.False(ZkVerifier.Verify(proof, point2, gen));
		}

		[Fact]
		public void VerifyLargeScalar()
		{
			uint val = int.MaxValue;
			var gen = new Scalar(4) * Generators.G;
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
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;

			var publicPoint = point1;
			var nonce = Generators.G;
			var challenge = ZkChallenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0x0F850F8CUL, 0x9D74683AUL, 0xDD03779BUL, 0xFD58F09BUL, 0xE148D87AUL, 0x3477A63FUL, 0xBE5906D3UL, 0x35E5A382UL }", challenge.ToC(""));

			publicPoint = Generators.G;
			nonce = point2;
			challenge = ZkChallenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0x69823107UL, 0xDA1CE96BUL, 0xBA00C8E7UL, 0x8A031437UL, 0x4D0BC9ADUL, 0x790E6FD8UL, 0x6C2EF5E6UL, 0x8F476E3FUL }", challenge.ToC(""));

			publicPoint = point1;
			nonce = point2;
			challenge = ZkChallenge.Build(publicPoint, nonce);
			Assert.Equal("secp256k1_scalar  = { 0xC5AA9243UL, 0x074DDA7CUL, 0xE8FFD6CAUL, 0xF3613B9EUL, 0x542CBD09UL, 0xF4191712UL, 0x045BD716UL, 0xECCC6626UL }", challenge.ToC(""));
		}

		[Fact]
		public void ChallengeThrows()
		{
			// Demonstrate when it shouldn't throw.
			ZkChallenge.Build(Generators.G, Generators.Ga);

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => ZkChallenge.Build(Generators.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkChallenge.Build(GroupElement.Infinity, Generators.Ga));
			Assert.ThrowsAny<ArgumentException>(() => ZkChallenge.Build(GroupElement.Infinity, GroupElement.Infinity));

			// Public and random points cannot be the same.
			Assert.ThrowsAny<InvalidOperationException>(() => ZkChallenge.Build(Generators.G, Generators.G));
		}

		[Fact]
		public void ExponentProofThrows()
		{
			// Demonstrate when it shouldn't throw.
			new ZkKnowledgeOfExponent(Generators.G, Scalar.One);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => new ZkKnowledgeOfExponent(Generators.G, Scalar.Zero));
			Assert.ThrowsAny<ArgumentException>(() => new ZkKnowledgeOfExponent(GroupElement.Infinity, Scalar.One));
			Assert.ThrowsAny<ArgumentException>(() => new ZkKnowledgeOfExponent(GroupElement.Infinity, Scalar.Zero));
		}

		[Fact]
		public void ProverThrows()
		{
			var two = new Scalar(2);

			// Demonstrate when it shouldn't throw.
			ZkProver.CreateProof(two, two * Generators.G, Generators.G);

			// Infinity or zero cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, two * Generators.G, Generators.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(two, GroupElement.Infinity, Generators.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(two, two * Generators.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, GroupElement.Infinity, Generators.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, two * Generators.G, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(two, GroupElement.Infinity, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(Scalar.Zero, GroupElement.Infinity, GroupElement.Infinity));

			// Public point must be generator * exponent.
			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(two, Generators.G, Generators.G));
			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(two, new Scalar(3) * Generators.G, Generators.G));
			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(two, Scalar.One * Generators.G, Generators.G));

			// Exponent cannot overflow.
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(EC.N, EC.N * Generators.G, Generators.G));
			Assert.ThrowsAny<ArgumentException>(() => ZkProver.CreateProof(ScalarLargestOverflow, ScalarLargestOverflow * Generators.G, Generators.G));
		}

		[Fact]
		public void VerifierThrows()
		{
			var proof = new ZkKnowledgeOfExponent(Generators.G, Scalar.One);

			// Demonstrate when it shouldn't throw.
			ZkVerifier.Verify(proof, Generators.Ga, Generators.Gg);

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => ZkVerifier.Verify(proof, GroupElement.Infinity, Generators.Gg));
			Assert.ThrowsAny<ArgumentException>(() => ZkVerifier.Verify(proof, Generators.Ga, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => ZkVerifier.Verify(proof, GroupElement.Infinity, GroupElement.Infinity));

			// Public point should not be equal to the random point of the proof.
			Assert.ThrowsAny<InvalidOperationException>(() => ZkVerifier.Verify(proof, Generators.G, Generators.Ga));
		}

		[Fact]
		public void RandomOverflow()
		{
			var mockRandom = new MockRandom();
			foreach (var scalar in GetScalars(x => x.IsOverflow))
			{
				mockRandom.GetScalarResults.Add(scalar);

				Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(Scalar.One, Scalar.One * Generators.G, Generators.G, mockRandom));
			}
		}

		[Fact]
		public void RandomZero()
		{
			var mockRandom = new MockRandom();
			mockRandom.GetScalarResults.Add(Scalar.Zero);

			// Don't tolerate if the second zero scalar random is received.
			mockRandom.GetScalarResults.Add(Scalar.Zero);

			Assert.ThrowsAny<InvalidOperationException>(() => ZkProver.CreateProof(Scalar.One, Scalar.One * Generators.G, Generators.G, mockRandom));
		}
	}
}
