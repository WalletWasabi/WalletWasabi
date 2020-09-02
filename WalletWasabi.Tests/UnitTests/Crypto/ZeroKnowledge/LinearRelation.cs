using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class EquationTest
	{
		[Theory]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(3, 5)]
		[InlineData(5, 7)]
		[InlineData(7, 11)]
		[InlineData(short.MaxValue, uint.MaxValue)]
		[InlineData(int.MaxValue, uint.MaxValue)]
		[InlineData(uint.MaxValue, uint.MaxValue)]
		public void VerifyResponsesAndSimulations(uint scalarSeed1, uint scalarSeed2)
		{
			var witness = new ScalarVector(new[] { new Scalar(scalarSeed1), new Scalar(scalarSeed2) });
			var generators = new GroupElementVector(new[] { Generators.G, Generators.Ga });
			var publicPoint = witness * generators;

			var eqn = new Equation(publicPoint, generators);

			// First, demonstrate proving knowledge with the witness
			var secretNonces = new ScalarVector(new[] { new Scalar(23), new Scalar(42) });
			var publicNonce = Enumerable.Zip(secretNonces, generators, (s, g) => s * g).Sum();
			var challenge = new Scalar(101);
			var response = eqn.Respond(witness, secretNonces, challenge);
			Assert.True(eqn.Verify(publicNonce, challenge, response));

			// Even without a witness, simulated proofs with the same response should still verify
			var simulatedNonce = eqn.Simulate(challenge, response);
			Assert.True(eqn.Verify(simulatedNonce, challenge, response));

			// And the simulated prover commitment should be the same as the real one
			// even if its discrete log w.r.t. the generators is not known
			Assert.True(simulatedNonce == publicNonce);

			// With a different challenge the nonce should be different
			var otherChallenge = new Scalar(103);
			var otherSimulatedNonce = eqn.Simulate(otherChallenge, response);
			Assert.True(eqn.Verify(otherSimulatedNonce, otherChallenge, response));
			Assert.True(otherSimulatedNonce != publicNonce);

			// And with a different response the verifier should still accept
			var otherResponse = new ScalarVector(new[] { new Scalar(2), new Scalar(3) });
			var thirdSimulatedNonce = eqn.Simulate(challenge, otherResponse);
			Assert.True(eqn.Verify(thirdSimulatedNonce, challenge, otherResponse));
			Assert.True(thirdSimulatedNonce != otherSimulatedNonce);
			Assert.True(thirdSimulatedNonce != publicNonce);

			// The verifying should reject invalid transcripts
			Assert.False(eqn.Verify(simulatedNonce, otherChallenge, response));
			Assert.False(eqn.Verify(publicNonce, otherChallenge, response));
		}

		[Fact]
		public void IgnoredWitnessComponents()
		{
			// Sometimes an equation uses the point at infinity as a generator,
			// effectively canceling out the corresponding component of the witness
			var generators = new GroupElementVector(new[] { Generators.G, GroupElement.Infinity });
			var publicPoint = new Scalar(42) * Generators.G;
			var eqn = new Equation(publicPoint, generators);

			var witness1 = new ScalarVector(new[] { new Scalar(42), new Scalar(23) });
			var witness2 = new ScalarVector(new[] { new Scalar(42), new Scalar(100) });

			// Generate a single nonce to be shared by both proofs.
			// note that in normal circumstances this is catastrophic because nonce
			// reuse with different challenges allows recovery of the witness.
			// in this case this is intentional, so that the test can compare the
			// responses which would otherwise be different.
			var secretNonces = new ScalarVector(new[] { new Scalar(7), new Scalar(11) });
			var publicNonce = Enumerable.Zip(secretNonces, generators, (s, g) => s * g).Sum();
			var challenge = new Scalar(13);

			// Derive two responses with the two different witnesses for the same
			// point, and ensure that both are valid, implying that the second
			// component in the witness is ignored.
			var response1 = eqn.Respond(witness1, secretNonces, challenge);
			Assert.True(eqn.Verify(publicNonce, challenge, response1));
			var response2 = eqn.Respond(witness2, secretNonces, challenge);
			Assert.True(eqn.Verify(publicNonce, challenge, response2));

			// With different witnesses the responses should be different even if the
			// nonces are the same, but since the first part of the witness is the
			// same that sub-response should be the same for the same nonce
			Assert.False(response1 == response2);
			Assert.True(response1.First() == response2.First());
		}
	}
}
