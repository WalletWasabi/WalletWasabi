using WalletWasabi.Helpers;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform
{
	public delegate VerifyResponse VerifierCommitToNonces(Proof proof);
	public delegate bool VerifyResponse();

	public class FSVerifier
	{
		public bool Verify(Statement statement, Proof proof)
		{
			Guard.NotNull(nameof(statement), statement);
			var transcript = new Transcript(new byte[] { }); // FIXME label

			var commitToNonces = CommitToStatements(statement, transcript);
			var respondToChallenge = commitToNonces(proof);
			var matches = respondToChallenge();
			return matches;
		}

		public VerifierCommitToNonces CommitToStatements(Statement statement, Transcript transcript)
		{
			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			transcript.CommitStatement(statement);

			return (proof) => CommitToNonces(statement, transcript, proof);
		}

		private VerifyResponse CommitToNonces(Statement statement, Transcript transcript, Proof proof)
		{
			// After all the statements have been committed, the public nonces are
			// added to the transcript. This is done separately from the statement
			// commitments because the prover derives these based on the compound
			// statements, and the verifier must add data to the transcript in the
			// same order as the prover.
			transcript.CommitPublicNonces(proof.PublicNonces);

			return () => VerifyResponse(statement, transcript, proof);
		}

		private bool VerifyResponse(Statement statement, Transcript transcript, Proof proof)
		{
			// After all the public nonces have been committed, a challenge can be
			// generated based on transcript state. Since challenges are deterministic
			// outputs of a hash function which depends on the prover commitments, the
			// verifier obtains the same challenge and then accept if the responses
			// satisfy the verification equation.
			var challenge = transcript.GenerateChallenge();
			return statement.CheckVerificationEquation(proof.PublicNonces, challenge, proof.Responses);
		}
	}
}
