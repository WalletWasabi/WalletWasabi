using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform
{
	public delegate VerifyResponse VerifierCommitToNonces(Proof proof);

	public delegate bool VerifyResponse();

	public class Verifier
	{
		public Verifier(LinearRelation.Statement statement)
		{
			Statement = statement;
		}

		// Although in principle this could be an interface type, since only linear
		// relations are needed for our proofs this is specialized to that type.
		public LinearRelation.Statement Statement { get; }

		public VerifierCommitToNonces CommitToStatements(Transcript transcript)
		{
			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			transcript.CommitStatement(Statement);

			return (Proof proof) => CommitToNonces(transcript, proof);
		}

		private VerifyResponse CommitToNonces(Transcript transcript, Proof proof)
		{
			// After all the statements have been committed, the public nonces are
			// added to the transcript. This is done separately from the statement
			// commitments because the prover derives these based on the compound
			// statements, and the verifier must add data to the transcript in the
			// same order as the prover.
			transcript.CommitPublicNonces(proof.PublicNonces);

			return () => VerifyResponse(transcript, proof);
		}

		private bool VerifyResponse(Transcript transcript, Proof proof)
		{
			// After all the public nonces have been committed, a challenge can be
			// generated based on transcript state. Since challenges are deterministic
			// outputs of a hash function which depends on the prover commitments, the
			// verifier obtains the same challenge and then accepts if the responses
			// satisfy the verification equation.
			var challenge = transcript.GenerateChallenge();
			return Statement.CheckVerificationEquation(proof.PublicNonces, challenge, proof.Responses);
		}
	}
}
