using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	/// <summary>
	/// This delegate represents the second phase of proof creation with <see
	/// cref="IKnowledge"/>.
	/// </summary>
	public delegate RespondToChallenge ProverCommitToNonces(WasabiRandom random);

	/// <summary>
	/// This delegate represents the third phase of proof creation with <see
	/// cref="IKnowledge"/>.
	/// </summary>
	public delegate Proof RespondToChallenge();

	/// <summary>
	/// <see cref="IKnowledge"/> supports creation of a non interactive proof
	/// based on a strong variant of the Fiat-Shamir transform
	/// <see cref="Transcript"/> by splitting the process into into three phases:
	/// <list type="number">
	/// <item>The first phase is to commit to all the statements and their public
	/// inputs using <see cref="CommitToStatements(Transcript)"/>, so that
	/// synthetic nonce generation for every sub-proof depends on the statement as
	/// a whole.</item>
	/// <item>The second phase is to generate and commit all the public nonces
	/// using <see cref="ProverCommitToNonces"/>, so that the challenges for each
	/// sub-proof depends on the nonces of all other statements.</item>
	/// <item>The third phase is to generate challenges based on the transcript
	/// and compute the responses to them using
	/// <see cref="RespondToChallenge"/>.</item>
	/// </list>
	/// </summary>
	public interface IKnowledge
	{
		// The first phase is to commit to all the statements, so that synthetic
		// nonce generation for every sub-proof depends on the statement as a whole
		ProverCommitToNonces CommitToStatements(Transcript transcript);

		// Provers must be convertible to corresponding verifiers
		IStatement ToStatement();
	}
}
