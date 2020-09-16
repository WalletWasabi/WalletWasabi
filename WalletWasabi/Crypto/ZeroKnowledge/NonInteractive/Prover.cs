using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;

namespace WalletWasabi.Crypto.ZeroKnowledge.NonInteractive
{
	public static class Prover
	{
		private delegate Proof DeferredProofCreator(Scalar challenge);

		public static IEnumerable<Proof> Prove(Transcript transcript, IEnumerable<Knowledge> knowledge, WasabiRandom random)
		{
			// Before anything else all components in a compound proof commit to the
			// individual sub-statement that will be proven, ensuring that the
			// challenges and therefore the responses depend on the statement as a
			// whole.
			foreach (var k in knowledge)
			{
				transcript.CommitStatement(k.Statement);
			}

			var deferredResponds = new List<DeferredProofCreator>();
			foreach (var k in knowledge)
			{
				// With all the statements committed, generate a vector of random secret
				// nonces for every equation in underlying proof system. In order to
				// ensure that nonces are never reused (e.g. due to an insecure RNG) with
				// different challenges which would leak the witness, these are generated
				// as synthetic nonces that also depend on the witness data.
				var secretNonceProvider = transcript.CreateSyntheticSecretNonceProvider(k.Witness, random);
				var secretNonces = secretNonceProvider.GetScalarVector();

				// The prover then commits to these, adding the corresponding public
				// points to the transcript.
				var equations = k.Statement.Equations;
				var publicNonces = new GroupElementVector(equations.Select(equation => secretNonces * equation.Generators));
				transcript.CommitPublicNonces(publicNonces);

				deferredResponds.Add((challenge) => new Proof(publicNonces, k.RespondToChallenge(challenge, secretNonces)));
			}

			// With the public nonces committed to the transcript the prover can then
			// derive a challenge that depend on the transcript state without needing
			// to interact with the verifier, but ensuring that they can't know the
			// challenge before the prover commitments are generated.
			var challenge = transcript.GenerateChallenge();
			return deferredResponds.Select(createProof => createProof(challenge));
		}
	}
}
