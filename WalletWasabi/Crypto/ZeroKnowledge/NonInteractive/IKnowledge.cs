using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	// IKnowledge splits proving into 3 phases
	// The second phase is to generate and commit to all the nonces
	public delegate RespondToChallenge ProverCommitToNonces(WasabiRandom random);

	// The third phase is to generate challenges and respond to them
	public delegate Proof RespondToChallenge();

	public interface IKnowledge
	{
		// The first phase is to commit to all the statements, so that synthetic
		// nonce generation for every sub-proof depends on the statement as a whole
		ProverCommitToNonces CommitToStatements(Transcript transcript);

		// Provers must be convertible to corresponding verifiers
		IStatement ToStatement();
	}
}
