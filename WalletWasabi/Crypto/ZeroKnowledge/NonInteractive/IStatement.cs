using WalletWasabi.Crypto;
using System.Collections.Generic;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	// IStatement splits verification into 3 phases
	// The second phase is to generate and commit to all the nonces
	public delegate VerifyResponse VerifierCommitToNonces(Proof proof);

	// The third phase is to generate challenges and respond to them
	public delegate bool VerifyResponse();

	public interface IStatement
	{
		// The first phase is to commit to all the statements, so that synthetic
		// nonce generation for every sub-proof depends on the statement as a whole
		VerifierCommitToNonces CommitToStatements(Transcript transcript);

		// Verifiers must be convertible to corresponding provers
		IKnowledge ToKnowledge(IEnumerable<ScalarVector> witnesses);
	}
}
