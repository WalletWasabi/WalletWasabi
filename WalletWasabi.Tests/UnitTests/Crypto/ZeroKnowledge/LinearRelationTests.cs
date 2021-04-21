using NBitcoin.Secp256k1;
using System;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class LinearRelationTests
	{
		[Theory]
		[InlineData(0, 0)]
		[InlineData(0, 1)]
		[InlineData(1, 0)]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(3, 5)]
		[InlineData(5, 7)]
		[InlineData(7, 11)]
		[InlineData(short.MaxValue, uint.MaxValue)]
		[InlineData(int.MaxValue, uint.MaxValue)]
		[InlineData(uint.MaxValue, uint.MaxValue)]
		public void VerifyResponses(uint scalarSeed1, uint scalarSeed2)
		{
			var witness = new ScalarVector(new Scalar(scalarSeed1), new Scalar(scalarSeed2));
			var generators = new GroupElementVector(Generators.G, Generators.Ga);
			var publicPoint = witness * generators;

			var equation = new Equation(publicPoint, generators);

			// First, demonstrate proving knowledge with the witness
			var secretNonces = new ScalarVector(new Scalar(23), new Scalar(42));
			var publicNonce = Enumerable.Zip(secretNonces, generators, (s, g) => s * g).Sum();
			var challenge = new Scalar(101);
			var response = Equation.Respond(witness, secretNonces, challenge);
			Assert.True(equation.Verify(publicNonce, challenge, response));

			var otherChallenge = new Scalar(103);

			// The verifier should reject invalid transcripts. This requires
			// an exception for when the public input is the point at infinity,
			// because with a different challenge the nonce should be different
			// but if the secret is 0, due to the absorption property the response
			// will be the same with the same nonce.
			if (scalarSeed1 != 0 && scalarSeed2 != 0)
			{
				Assert.False(equation.Verify(publicNonce, otherChallenge, response));
			}

			// Modifying the response should invalidate the equation
			var modifiedResponse = new ScalarVector(response.Select((x, i) => i == 0 ? x + Scalar.One : x));
			Assert.False(equation.Verify(publicNonce, challenge, modifiedResponse));
			Assert.True(equation.Verify(publicNonce + Generators.G, challenge, modifiedResponse));

			// A proof made with a different witness must be rejected. If the
			// challenge is 0 (negligible probability with Fiat-Shamir under ROM) in
			// which case any witness satisfies the equation, but the verifier should
			// still reject.
			var badWitness = new ScalarVector(new Scalar(scalarSeed1 + 1), new Scalar(scalarSeed2));
			Assert.False(equation.Verify(publicNonce, challenge, Equation.Respond(badWitness, secretNonces, challenge)));
			Assert.False(equation.Verify(publicNonce, Scalar.Zero, Equation.Respond(badWitness, secretNonces, Scalar.Zero)));

			// A proof made with different secret nonces must also be rejected, unless
			// the public nonce is tweaked (not possible with Fiat-Shamir without a
			// 2nd pre-image attack on the hash function).
			var modifiedSecretNonces = new ScalarVector(secretNonces.Select((x, i) => i == 0 ? x + Scalar.One : x));
			var modifiedNonceResponse = Equation.Respond(witness, modifiedSecretNonces, challenge);
			Assert.False(equation.Verify(publicNonce, challenge, modifiedNonceResponse));
			Assert.True(equation.Verify(publicNonce + Generators.G, challenge, modifiedNonceResponse));
		}

		[Fact]
		public void UnsafeSecretNoncesThrow()
		{
			var x = new Scalar(42);
			var a = x * Generators.Gg;
			var knowledge = new Knowledge(new Statement(a, Generators.Gg), new ScalarVector(x));
			Assert.ThrowsAny<ArgumentException>(() => knowledge.RespondToChallenge(Scalar.One, new ScalarVector(Scalar.Zero)));
		}

		[Fact]
		public void IgnoredWitnessComponents()
		{
			// Sometimes an equation uses the point at infinity as a generator,
			// effectively canceling out the corresponding component of the witness
			var generators = new GroupElementVector(Generators.G, GroupElement.Infinity);
			var publicPoint = new Scalar(42) * Generators.G;
			var equation = new Equation(publicPoint, generators);

			var witness1 = new ScalarVector(new Scalar(42), new Scalar(23));
			var witness2 = new ScalarVector(new Scalar(42), new Scalar(100));

			// Generate a single nonce to be shared by both proofs.
			// note that in normal circumstances this is catastrophic because nonce
			// reuse with different challenges allows recovery of the witness.
			// in this case this is intentional, so that the test can compare the
			// responses which would otherwise be different.
			var secretNonces = new ScalarVector(new Scalar(7), new Scalar(11));
			var publicNonce = Enumerable.Zip(secretNonces, generators, (s, g) => s * g).Sum();
			var challenge = new Scalar(13);

			// Derive two responses with the two different witnesses for the same
			// point, and ensure that both are valid, implying that the second
			// component in the witness is ignored.
			var response1 = Equation.Respond(witness1, secretNonces, challenge);
			Assert.True(equation.Verify(publicNonce, challenge, response1));
			var response2 = Equation.Respond(witness2, secretNonces, challenge);
			Assert.True(equation.Verify(publicNonce, challenge, response2));

			// With different witnesses the responses should be different even if the
			// nonces are the same, but since the first part of the witness is the
			// same that sub-response should be the same for the same nonce
			Assert.False(response1 == response2);
			Assert.True(response1.First() == response2.First());
		}

		[Fact]
		public void StatementAndKnowledge()
		{
			var x = new Scalar(42);
			var a = x * Generators.Gg;
			var b = x * Generators.Gh;

			// Discrete log equality (Chaum-Pedersen proof)
			var statement = new Statement(new GroupElement[,]
			{
				{ a, Generators.Gg },
				{ b, Generators.Gh },
			});

			var challenge = new Scalar(13);

			// Create transcripts using a witness to the relation
			var knowledge = new Knowledge(statement, new ScalarVector(x));
			var secretNonces = new ScalarVector(new Scalar(7));
			var publicNonces = new GroupElementVector(statement.Equations.Select(equation => secretNonces * equation.Generators));
			var responses = knowledge.RespondToChallenge(challenge, secretNonces);
			Assert.Single(responses);
			Assert.True(statement.CheckVerificationEquation(publicNonces, challenge, responses));

			// Ensure that verifier rejects invalid transcripts
			Assert.False(statement.CheckVerificationEquation(publicNonces, new Scalar(17), responses));

			// Ensure that verifier rejects when proofs have been altered but not
			// when they are valid (which are prevented using Fiat-Shamir transform)
			var modifiedNonces = new GroupElementVector(publicNonces.First() + Generators.Gg, publicNonces.Last() + Generators.Gh);
			var modifiedResponses = new ScalarVector(responses.Select(x => x + Scalar.One));
			Assert.True(statement.CheckVerificationEquation(modifiedNonces, challenge, modifiedResponses));
			Assert.False(statement.CheckVerificationEquation(modifiedNonces, challenge, responses));
			Assert.False(statement.CheckVerificationEquation(publicNonces, challenge, modifiedResponses));
		}

		[Fact]
		public void KnowledgeThrows()
		{
			var x = new Scalar(42);
			var a = x * Generators.Gg;
			var b = x * Generators.Gh;

			var statement = new Statement(new GroupElement[,]
			{
				{ a, Generators.Gg },
				{ b, Generators.Gh },
			});

			// The witness should have the same number of components as the number of generators in the equations
			var ex = Assert.ThrowsAny<ArgumentException>(() => new Knowledge(statement, new ScalarVector(Scalar.Zero, Scalar.Zero)));
			Assert.Contains("size does not match", ex.Message);

			// Incorrect witness (multiplying by generators does not produce the public point)
			ex = Assert.ThrowsAny<ArgumentException>(() => new Knowledge(statement, new ScalarVector(Scalar.One)).AssertSoundness());
			Assert.Contains("witness is not solution of the equation", ex.Message);

			// Incorrect statement generators (effectively incorrect witness)
			var badStatement = new Statement(new GroupElement[,]
			{
				{ a, Generators.Gh },
				{ b, Generators.Gg },
			});
			ex = Assert.ThrowsAny<ArgumentException>(() => new Knowledge(badStatement, new ScalarVector(Scalar.One)).AssertSoundness());
			Assert.Contains("witness is not solution of the equation", ex.Message);
		}
	}
}
