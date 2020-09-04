using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform
{
	public class Knowledge : IKnowledge
	{
		// Although in principle this could be an interface type, since only one
		// type of statement is needed for our proofs this is specialized to only
		// that type.
		public Knowledge(LinearRelation.Knowledge knowledge)
		{
			LinearRelation = Guard.NotNull(nameof(knowledge), knowledge);
		}

		public LinearRelation.Knowledge LinearRelation { get; }

		public IStatement ToStatement()
		{
			return new FiatShamirTransform.Statement(LinearRelation.Statement);
		}

		public ProverCommitToNonces CommitToStatements(Transcript transcript)
		{
			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			transcript.CommitStatement(LinearRelation.Statement);

			return delegate(WasabiRandom random)
			{
				return this.CommitToNonces(transcript, random);
			};
		}

		private RespondToChallenge CommitToNonces(Transcript transcript,  WasabiRandom random)
		{
			// With all the statements committed, generate a vector of random secret
			// nonces for every equation in underlying proof system. In order to
			// ensure that nonces are never reused (e.g. due to an insecure RNG) with
			// different challenges which would leak the witness, these are generated
			// as synthetic nonces that also depend on the witness data.
			var secretNonceProvider = transcript.CreateSyntheticSecretNonceProvider(LinearRelation.Witness, random);

			// Actually generate all of the required nonces and save them in an array
			// because if the enumerable is evaluated several times the results will
			// be different.
			// Note, ToArray() is needed to make sure that secretNonces is captured
			// once and shared between the phases.
			var equations = LinearRelation.Statement.Equations;
			var secretNonces = secretNonceProvider.Sequence.Take(equations.Count()).ToArray();

			// The prover then commits to these, adding the corresponding public
			// points to the transcript.
			var publicNonces = new GroupElementVector(equations.Zip(secretNonces, (equation, pointSecretNonces) => pointSecretNonces * equation.Generators));
			transcript.CommitPublicNonces(publicNonces);

			return delegate
			{
				return this.Respond(transcript, publicNonces, secretNonces);
			};
		}

		private Proof Respond(Transcript transcript, GroupElementVector nonces, IEnumerable<ScalarVector> secretNonces)
		{
			// With the public nonces committed to the transcript the prover can then
			// derive a challenge that depend on the transcript state without needing
			// to interact with the verifier, but ensuring that they can't know the
			// challenge before the prover commitments are generated.
			var challenge = transcript.GenerateChallenge();
			var responses = LinearRelation.RespondToChallenge(challenge, secretNonces);
			return new Proof(nonces, responses);
		}
	}
}
