using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
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

			Scalar randomScalar = new Scalar(14);
			var nonce = randomScalar * generator;
			var statement = new Statement(publicPoint, generator);
			var challenge = Challenge.Build(nonce, statement);

			var response = randomScalar + (secret + Scalar.One) * challenge;
			var proof = new KnowledgeOfDlog(nonce, response);
			Assert.False(Verifier.Verify(proof, statement));

			// Other false verification tests.
			var point1 = new Scalar(3) * Generators.G;
			var point2 = new Scalar(7) * Generators.G;
			var scalar = new Scalar(11);
			var gen = Generators.G;

			proof = new KnowledgeOfDlog(point1, scalar);
			var statement2 = new Statement(point2, gen);
			Assert.False(Verifier.Verify(proof, statement2));
		}

		[Fact]
		public void DoesntThrowForFormatIssues()
		{
			// Verification should not throw, it should return false instead.
			// If there's a format issue then returning false makes sense, as the API user expects that.
			var dlProof = new KnowledgeOfDlog(Generators.G, Scalar.One);
			var repProof = new KnowledgeOfRep(Generators.G, Scalar.One, CryptoHelpers.ScalarThree);

			// Demonstrate when it should be true.
			var validStatement1 = new Statement(Generators.Ga, Generators.Gg);
			var validStatement2 = new Statement(Generators.Ga, Generators.Gg, Generators.Ga);
			Verifier.Verify(dlProof, validStatement1);
			Verifier.Verify(repProof, validStatement2);

			// Public point should not be equal to the random point of the proof.
			Assert.False(Verifier.Verify(dlProof, new Statement(Generators.G, Generators.Ga)));
			Assert.False(Verifier.Verify(repProof, new Statement(Generators.G, Generators.Gg, Generators.Ga)));

			// Same number of generators must be provided as the responses.
			Assert.False(Verifier.Verify(dlProof, new Statement(Generators.Ga, Generators.Gg, Generators.GV)));
			Assert.False(Verifier.Verify(repProof, new Statement(Generators.Ga, Generators.Gg)));
			Assert.False(Verifier.Verify(repProof, new Statement(Generators.Ga, Generators.Gg, Generators.Ga, Generators.GV)));
		}

		[Fact]
		public void KnowledgeOfAndVerifiesToFalse()
		{
			// First demonstrate when it shouldn't fail.
			var secrets1 = new[] { CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo };
			var secrets2 = new[] { CryptoHelpers.ScalarThree, CryptoHelpers.ScalarLarge };

			var generators1 = new[] { Generators.G, Generators.Ga };
			var generators2 = new[] { Generators.G, Generators.Ga };

			Statement statement1 = CreateStatement(secrets1, generators1);
			Statement statement2 = CreateStatement(secrets2, generators2);

			var proof = Prover.CreateAndProof(new[]
			{
				new KnowledgeOfRepParams(secrets1, statement1),
				new KnowledgeOfRepParams(secrets2, statement2)
			});

			Assert.True(Verifier.Verify(proof, new[] { statement1, statement2 }));

			// Change the order of the statements:
			Assert.False(Verifier.Verify(proof, new[] { statement2, statement1 }));
			// Change a generator of a statement.
			Assert.True(Verifier.Verify(proof, new[] { CreateStatement(secrets1, new[] { Generators.G, Generators.Ga }), statement2 }));
			Assert.False(Verifier.Verify(proof, new[] { CreateStatement(secrets1, new[] { Generators.G, Generators.G }), statement2 }));
			// Change the number of generators.
			Assert.False(Verifier.Verify(proof, new[] { CreateStatement(secrets1, new[] { Generators.G }), statement2 }));
			Assert.False(Verifier.Verify(proof, new[] { CreateStatement(secrets1, new[] { Generators.G, Generators.Ga, Generators.Gg }), statement2 }));
			// Change a secret.
			Assert.True(Verifier.Verify(proof, new[] { CreateStatement(new[] { CryptoHelpers.ScalarOne, CryptoHelpers.ScalarTwo }, generators1), statement2 }));
			Assert.False(Verifier.Verify(proof, new[] { CreateStatement(new[] { CryptoHelpers.ScalarTwo, CryptoHelpers.ScalarTwo }, generators1), statement2 }));

			// Create a proof from two individually valid proofs, but not an AND proof.
			var p1 = Prover.CreateProof(new KnowledgeOfRepParams(secrets1, statement1));
			var p2 = Prover.CreateProof(new KnowledgeOfRepParams(secrets2, statement2));
			Assert.True(Verifier.Verify(p1, statement1));
			Assert.True(Verifier.Verify(p2, statement2));
			Assert.False(Verifier.Verify(new KnowledgeOfAnd(new[] { p1, p2 }), new[] { statement2, statement1 }));
		}

		private static Statement CreateStatement(Scalar[] secrets1, GroupElement[] generators1)
		{
			var publicPoint = GroupElement.Infinity;
			var secretGeneratorPairs = secrets1.Zip(generators1);
			foreach (var (secret, generator) in secretGeneratorPairs)
			{
				publicPoint += secret * generator;
			}
			return new Statement(publicPoint, generators1);
		}
	}
}
