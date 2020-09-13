using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class FiatShamirTransformTests
	{
		[Fact]
		public void FiatShamirComposition()
		{
			var rnd = new MockRandom();
			rnd.GetBytesResults.Add(new byte[32]);
			rnd.GetBytesResults.Add(new byte[32]);

			var witness1 = new ScalarVector(Scalar.One);
			var witness2 = new ScalarVector(Scalar.One + Scalar.One);

			var g = new GroupElementVector(Generators.G);

			var publicPoint1 = witness1 * g;
			var publicPoint2 = witness2 * g;

			var statement1 = new Statement(new Equation(publicPoint1, g));
			var statement2 = new Statement(new Equation(publicPoint2, g));

			var prover1 = new Prover(new Knowledge(statement1, witness1));
			var prover2 = new Prover(new Knowledge(statement2, witness2));

			var proverTranscript = new WalletWasabi.Crypto.ZeroKnowledge.Transcript(new byte[0]);
			var verifierTranscript = proverTranscript.MakeCopy();

			var prover1Nonces = prover1.CommitToStatements(proverTranscript);
			var prover2Nonces = prover2.CommitToStatements(proverTranscript);

			var prover1Respond = prover1Nonces(rnd);
			var prover2Respond = prover2Nonces(rnd);

			var proof1 = prover1Respond();
			var proof2 = prover2Respond();

			var verifier1 = new Verifier(statement1);
			var verifier2 = new Verifier(statement2);

			// First, verify as a compound proof
			var correctVerifierTranscript = verifierTranscript.MakeCopy();
			var correctVerifier1Nonces = verifier1.CommitToStatements(correctVerifierTranscript);
			var correctVerifier2Nonces = verifier2.CommitToStatements(correctVerifierTranscript);
			var correctVerifier1Verify = correctVerifier1Nonces(proof1);
			var correctVerifier2Verify = correctVerifier2Nonces(proof2);
			Assert.True(correctVerifier1Verify());
			Assert.True(correctVerifier2Verify());

			// If the verifiers are not run interleaved, they should reject.
			var notInterleavedVerifierTranscript = verifierTranscript.MakeCopy();
			var notInterleavedVerifier1Nonces = verifier1.CommitToStatements(correctVerifierTranscript);
			var notInterleavedVerifier1Verify = notInterleavedVerifier1Nonces(proof1);
			Assert.False(notInterleavedVerifier1Verify());
			var notInterleavedVerifier2Nonces = verifier2.CommitToStatements(correctVerifierTranscript);
			var notInterleavedVerifier2Verify = notInterleavedVerifier2Nonces(proof2);
			Assert.False(notInterleavedVerifier2Verify());

			// If the verifiers are run independently (without sharing a transcript),
			// they should reject.
			var incompleteTranscript1 = verifierTranscript.MakeCopy();
			var incompleteTranscript2 = verifierTranscript.MakeCopy();
			var incompleteTranscriptVerifier1Nonces = verifier1.CommitToStatements(incompleteTranscript1);
			var incompleteTranscriptVerifier2Nonces = verifier2.CommitToStatements(incompleteTranscript2);
			var incompleteTranscriptVerifier1Verify = incompleteTranscriptVerifier1Nonces(proof1);
			var incompleteTranscriptVerifier2Verify = incompleteTranscriptVerifier2Nonces(proof2);
			Assert.False(incompleteTranscriptVerifier1Verify());
			Assert.False(incompleteTranscriptVerifier2Verify());

			// If the sub-proofs are swapped between the verifiers, they should reject.
			var incorrectProofVerifierTranscript = verifierTranscript.MakeCopy();
			var incorrectProofVerifier1Nonces = verifier1.CommitToStatements(correctVerifierTranscript);
			var incorrectProofVerifier2Nonces = verifier2.CommitToStatements(correctVerifierTranscript);
			var incorrectProofVerifier1Verify = incorrectProofVerifier1Nonces(proof2);
			var incorrectProofVerifier2Verify = incorrectProofVerifier2Nonces(proof1);
			Assert.False(incorrectProofVerifier1Verify());
			Assert.False(incorrectProofVerifier2Verify());

			// If the order of the verifiers is changed, they should reject.
			var incorrectOrderVerifierTranscript = verifierTranscript.MakeCopy();
			var incorrectOrderVerifier1Nonces = verifier1.CommitToStatements(correctVerifierTranscript);
			var incorrectOrderVerifier2Nonces = verifier2.CommitToStatements(correctVerifierTranscript);
			var incorrectOrderVerifier2Verify = incorrectOrderVerifier2Nonces(proof2);
			var incorrectOrderVerifier1Verify = incorrectOrderVerifier1Nonces(proof1);
			Assert.False(incorrectOrderVerifier1Verify());
			Assert.False(incorrectOrderVerifier2Verify());

			// If the proofs are committed to the transcript in the right order but
			// with the wrong verifier (combination of previous two cases) they should
			// reject.
			var incorrectOrderAndProofVerifierTranscript = verifierTranscript.MakeCopy();
			var incorrectOrderAndProofVerifier1Nonces = verifier1.CommitToStatements(correctVerifierTranscript);
			var incorrectOrderAndProofVerifier2Nonces = verifier2.CommitToStatements(correctVerifierTranscript);
			var incorrectOrderAndProofVerifier2Verify = incorrectOrderAndProofVerifier2Nonces(proof1);
			var incorrectOrderAndProofVerifier1Verify = incorrectOrderAndProofVerifier1Nonces(proof2);
			Assert.False(incorrectOrderAndProofVerifier2Verify());
			Assert.False(incorrectOrderAndProofVerifier1Verify());
		}
	}
}
