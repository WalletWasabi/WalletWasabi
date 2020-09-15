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
			var response = equation.Respond(witness, secretNonces, challenge);
			Assert.True(equation.Verify(publicNonce, challenge, response));

			// With a different challenge the nonce should be different
			// unless the secret is 0, due to the absorption property
			var otherChallenge = new Scalar(103);

			// The verifying should reject invalid transcripts, and this also requires
			// an exception for when the public input is the point at infinity
			if (scalarSeed1 != 0 && scalarSeed2 != 0)
			{
				Assert.False(equation.Verify(publicNonce, otherChallenge, response));
			}
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
			var response1 = equation.Respond(witness1, secretNonces, challenge);
			Assert.True(equation.Verify(publicNonce, challenge, response1));
			var response2 = equation.Respond(witness2, secretNonces, challenge);
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
			var statement = new Statement(
				new Equation(a, new GroupElementVector(Generators.Gg)),
				new Equation(b, new GroupElementVector(Generators.Gh))
			);

			var challenge = new Scalar(13);

			// Create transcripts using a witness to the relation
			var knowledge = new Knowledge(statement, new ScalarVector(x));
			var allSecretNonces = new[] { new ScalarVector(new Scalar(7)), new ScalarVector(new Scalar(11)) };
			var publicNonces = new GroupElementVector(statement.Equations.Zip(allSecretNonces, (equation, secretNonces) => secretNonces * equation.Generators));
			var allResponses = knowledge.RespondToChallenge(challenge, allSecretNonces);
			Assert.True(statement.CheckVerificationEquation(publicNonces, challenge, allResponses));

			// Ensure that verifier rejects invalid transcripts
			Assert.False(statement.CheckVerificationEquation(publicNonces, new Scalar(17), allResponses));
		}

		[Fact]
		public void StatementThrows()
		{
			// Jagged equation matrices are not allowed
			Assert.ThrowsAny<ArgumentException>(() => new Statement(
				new Equation(GroupElement.Infinity, new GroupElementVector(Generators.Gg, Generators.Gh)),
				new Equation(GroupElement.Infinity, new GroupElementVector(Generators.G))
			));

			// Null generators are not allowed
			Assert.ThrowsAny<ArgumentException>(() => new Statement(
				new Equation(GroupElement.Infinity, new GroupElementVector(Generators.Gg, Generators.Gh)),
				new Equation(GroupElement.Infinity, new GroupElementVector(Generators.G, null!))
			));
		}

		[Fact]
		public void KnowledgeThrows()
		{
			var x = new Scalar(42);
			var a = x * Generators.Gg;
			var b = x * Generators.Gh;

			var statement = new Statement(
				new Equation(a, new GroupElementVector(Generators.Gg)),
				new Equation(b, new GroupElementVector(Generators.Gh))
			);

			// The witness should have the same number of components as the number of generators in the equations
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(statement, new ScalarVector(Scalar.Zero, Scalar.Zero)));

			// Incorrect witness (multiplying by generators does not produce the public point)
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(statement, new ScalarVector(Scalar.One)));

			// Incorrect statement generators (effectively incorrect witness)
			var badStatement = new Statement(
				new Equation(a, new GroupElementVector(Generators.Gh)),
				new Equation(b, new GroupElementVector(Generators.Gg))
			);
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(badStatement, new ScalarVector(Scalar.One)));
		}
	}
}
