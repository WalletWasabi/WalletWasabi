using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform
{
	public delegate RespondToChallenge ProverCommitToNonces(WasabiRandom random);

	public delegate Proof RespondToChallenge();

	public class FSProver
	{
		// Although in principle this could be an interface type, since only one
		// type of statement is needed for our proofs this is specialized to only
		// that type.
		public Proof Prove(Knowledge knowledge)
		{
			Guard.NotNull(nameof(knowledge), knowledge);
			var transcript = new Transcript(new byte[] { }); // FIXME label
			var random = new SecureRandom();

			var commitToNonces = CommitToStatements(knowledge, transcript);
			var respondToChallenge = commitToNonces(random);
			var proof = respondToChallenge();
			return proof;
		}

		public ProverCommitToNonces CommitToStatements(Knowledge knowledge, Transcript transcript)
		{
			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			transcript.CommitStatement(knowledge.Statement);

			return (random) => CommitToNonces(knowledge, transcript, random);
		}

		private RespondToChallenge CommitToNonces(Knowledge knowledge, Transcript transcript, WasabiRandom random)
		{
			// With all the statements committed, generate a vector of random secret
			// nonces for every equation in underlying proof system. In order to
			// ensure that nonces are never reused (e.g. due to an insecure RNG) with
			// different challenges which would leak the witness, these are generated
			// as synthetic nonces that also depend on the witness data.
			var secretNonceProvider = transcript.CreateSyntheticSecretNonceProvider(knowledge.Witness, random);

			// Actually generate all of the required nonces and save them in an array
			// because if the enumerable is evaluated several times the results will
			// be different.
			// Note, ToArray() is needed to make sure that secretNonces is captured
			// once and shared between the phases.
			var equations = knowledge.Statement.Equations;
			var secretNonces = secretNonceProvider.Sequence.Take(equations.Count()).ToArray();

			// The prover then commits to these, adding the corresponding public
			// points to the transcript.
			var publicNonces = new GroupElementVector(equations.Zip(secretNonces, (equation, pointSecretNonces) => pointSecretNonces * equation.Generators));
			transcript.CommitPublicNonces(publicNonces);

			return () => Respond(knowledge, transcript, publicNonces, secretNonces);
		}

		private Proof Respond(Knowledge knowledge, Transcript transcript, GroupElementVector nonces, IEnumerable<ScalarVector> secretNonces)
		{
			// With the public nonces committed to the transcript the prover can then
			// derive a challenge that depend on the transcript state without needing
			// to interact with the verifier, but ensuring that they can't know the
			// challenge before the prover commitments are generated.
			var challenge = transcript.GenerateChallenge();
			var responses = knowledge.RespondToChallenge(challenge, secretNonces);
			return new Proof(nonces, responses);
		}
	}
}
