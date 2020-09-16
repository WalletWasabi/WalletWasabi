using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform
{
	public delegate RespondToChallenge ProverCommitToNonces(WasabiRandom random);

	public delegate Proof RespondToChallenge();

	public class Prover
	{
		// Although in principle this could be an interface type, since only one
		// type of statement is needed for our proofs this is specialized to only
		// that type.
		public Prover(LinearRelation.Knowledge knowledge)
		{
			Knowledge = knowledge;
		}

		public LinearRelation.Knowledge Knowledge { get; }

		public ProverCommitToNonces CommitToStatements(Transcript transcript)
		{
			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			transcript.CommitStatement(Knowledge.Statement);

			return (WasabiRandom random) => CommitToNonces(transcript, random);
		}

		private RespondToChallenge CommitToNonces(Transcript transcript, WasabiRandom random)
		{
			// With all the statements committed, generate a vector of random secret
			// nonces for every equation in underlying proof system. In order to
			// ensure that nonces are never reused (e.g. due to an insecure RNG) with
			// different challenges which would leak the witness, these are generated
			// as synthetic nonces that also depend on the witness data.
			var secretNonceProvider = transcript.CreateSyntheticSecretNonceProvider(Knowledge.Witness, random);
			var secretNonces = secretNonceProvider.GetScalarVector();

			// The prover then commits to these, adding the corresponding public
			// points to the transcript.
			var equations = Knowledge.Statement.Equations;
			var publicNonces = new GroupElementVector(equations.Select(equation => secretNonces * equation.Generators));
			transcript.CommitPublicNonces(publicNonces);

			return () => Respond(transcript, publicNonces, secretNonces);
		}

		private Proof Respond(Transcript transcript, GroupElementVector nonces, ScalarVector secretNonces)
		{
			// With the public nonces committed to the transcript the prover can then
			// derive a challenge that depend on the transcript state without needing
			// to interact with the verifier, but ensuring that they can't know the
			// challenge before the prover commitments are generated.
			var challenge = transcript.GenerateChallenge();
			var responses = Knowledge.RespondToChallenge(challenge, secretNonces);
			return new Proof(nonces, responses);
		}
	}
}
