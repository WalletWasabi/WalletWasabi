using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class VerifierTests
	{
		[Fact]
		public void KnowledgeOfDiscreteLogVerifiesToFalse()
		{
			// Even if the challenge is correct, because the public input in the hash is right,
			// if the final response is not valid wrt the verification equation,
			// the verifier should still reject.
			var secret = new Scalar(7);
			var generator = Generators.G;
			var publicPoint = secret * generator;

			var transcript = new Transcript();
			transcript.Statement(Encoding.UTF8.GetBytes(Prover.KnowledgeOfRepresentationTag), publicPoint, generator);

			var mockRandom = new MockRandom();
			mockRandom.GetBytesResults.Add(new byte[32]);
			Scalar randomScalar = transcript.GenerateNonce(secret, mockRandom);

			// synthetic nonce should still include a hash of the state
			Assert.NotEqual(randomScalar, Scalar.Zero);
			Assert.NotEqual(randomScalar, Scalar.One);
			Assert.NotEqual(randomScalar, secret);

			var nonce = randomScalar * generator;

			transcript.NonceCommitment(nonce);

			var challenge = transcript.GenerateChallenge();

			var response = randomScalar + (secret + Scalar.One) * challenge;
			var proof = new KnowledgeOfDiscreteLog(nonce, response);
			Assert.False(Verifier.Verify(proof, publicPoint, generator));

			// Other false verification tests.
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var scalar = new Scalar(11);
			var gen = Generators.G;

			proof = new KnowledgeOfDiscreteLog(point1, scalar);
			Assert.False(Verifier.Verify(proof, point2, gen));
		}

		[Fact]
		public void Throws()
		{
			var dlProof = new KnowledgeOfDiscreteLog(Generators.G, Scalar.One);
			var repProof = new KnowledgeOfRepresentation(Generators.G, Scalar.One, CryptoHelpers.ScalarThree);

			// Demonstrate when it shouldn't throw.
			Verifier.Verify(dlProof, Generators.Ga, Generators.Gg);
			Verifier.Verify(repProof, Generators.Ga, Generators.Gg, Generators.Ga);

			// At least one generator must be provided.
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(dlProof, Generators.Ga));
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(repProof, Generators.Ga));

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(dlProof, GroupElement.Infinity, Generators.Gg));
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(dlProof, Generators.Ga, GroupElement.Infinity));
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(dlProof, GroupElement.Infinity, GroupElement.Infinity));

			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(repProof, GroupElement.Infinity, Generators.Gg, Generators.Ga));
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(repProof, Generators.Ga, GroupElement.Infinity, Generators.Ga));
			Assert.ThrowsAny<ArgumentException>(() => Verifier.Verify(repProof, Generators.Ga, Generators.Gg, GroupElement.Infinity));

			// Public point should not be equal to the random point of the proof.
			Assert.ThrowsAny<InvalidOperationException>(() => Verifier.Verify(dlProof, Generators.G, Generators.Ga));
			Assert.ThrowsAny<InvalidOperationException>(() => Verifier.Verify(repProof, Generators.G, Generators.Gg, Generators.Ga));

			// Same number of generators must be provided as the responses.
			Assert.ThrowsAny<InvalidOperationException>(() => Verifier.Verify(dlProof, Generators.Ga, Generators.Gg, Generators.GV));
			Assert.ThrowsAny<InvalidOperationException>(() => Verifier.Verify(repProof, Generators.Ga, Generators.Gg));
			Assert.ThrowsAny<InvalidOperationException>(() => Verifier.Verify(repProof, Generators.Ga, Generators.Gg, Generators.Ga, Generators.GV));
		}
	}
}
