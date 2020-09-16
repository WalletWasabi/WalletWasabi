using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	public static class Verifier
	{
		public static bool Verify(Transcript transcript, IEnumerable<LinearRelation.Statement> statements, IEnumerable<Proof> proofs)
		{
			Guard.Same(nameof(proofs), proofs.Count(), statements.Count());

			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			foreach (var statement in statements)
			{
				transcript.CommitStatement(statement);
			}

			// After all the statements have been committed, the public nonces are
			// added to the transcript. This is done separately from the statement
			// commitments because the prover derives these based on the compound
			// statements, and the verifier must add data to the transcript in the
			// same order as the prover.
			foreach (var proof in proofs)
			{
				transcript.CommitPublicNonces(proof.PublicNonces);
			}

			// After all the public nonces have been committed, a challenge can be
			// generated based on transcript state. Since challenges are deterministic
			// outputs of a hash function which depends on the prover commitments, the
			// verifier obtains the same challenge and then accepts if the responses
			// satisfy the verification equation.
			var challenge = transcript.GenerateChallenge();

			return Enumerable.Zip(statements, proofs, (s, p) => s.CheckVerificationEquation(p.PublicNonces, challenge, p.Responses)).All(x => x);
		}
	}
}
