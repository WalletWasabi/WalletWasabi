using WalletWasabi.Crypto;
using System.Collections.Generic;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	/// <summary>
	/// This delegate represents the second phase of proof verification with <see
	/// cref="IStatement"/>.
	/// </summary>
	public delegate VerifyResponse VerifierCommitToNonces(Proof proof);

	/// <summary>
	/// This delegate represents the third phase of proof verification with <see
	/// cref="IStatement"/>.
	/// </summary>
	public delegate bool VerifyResponse();

	/// <summary>
	/// <see cref="IStatement"/> splits verification of a non interactive proof using
	/// into three phases corresponding to those of <see cref="IKnowledge"/>:
	/// <list type="number">

	/// <see cref="IStatement"/> supports the verification of a non-interactive
	/// proof created by a corresponding <see cref="IKnowledge"/> by splitting the
	/// process into into the same three phase structure:
	/// <list type="number">
	/// <item>The first phase is to commit to all the statements and their public
	/// inputs using <see cref="CommitToStatements(Transcript)"/>, so that
	/// the prover's synthetic nonce generation for every sub-proof depends on the
	/// statement as a whole.</item>
	/// <item>The second phase is to commit all the public nonces using
	/// <see cref="VerifierCommitToNonces"/>, so that the challenges for each
	/// sub-proof depends on the nonces of all other statements.</item>
	/// <item>The third phase is to generate challenges based on the transcript
	/// and verify the responses to those challenges with respect to the public
	/// nonce commitments using <see cref="VerifyResponse"/>.</item>
	/// </list>
	/// </summary>
	public interface IStatement
	{
		VerifierCommitToNonces CommitToStatements(Transcript transcript);

		// Verifiers must be convertible to corresponding provers
		IKnowledge ToKnowledge(IEnumerable<ScalarVector> witnesses);
	}
}
