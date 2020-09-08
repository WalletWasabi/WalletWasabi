using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive.FiatShamirTransform;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class Proofs
	{
		public static bool CheckProof(Verifier verifier, Proof proof)
		{
			var transcript = new Transcript(new byte[] { }); // FIXME label

			var CommitToNonces = verifier.CommitToStatements(transcript);
			var VerifyResponse = CommitToNonces(proof);
			return VerifyResponse();
		}

		public static Proof CreateProof(Prover prover, WasabiRandom random)
		{
			var transcript = new Transcript(new byte[] { }); // FIXME label

			var CommitToNonces = prover.CommitToStatements(transcript);
			var RespondToChallenge = CommitToNonces(random);
			var proof = RespondToChallenge();

			if (!CheckProof(prover.ToVerifier(), proof))
			{
				throw new InvalidOperationException($"Prover or verifier is incorrectly implemented. Proof was built, but verification failed.");
			}

			return proof;
		}

		// Syntactic sugar
		public static Proof CreateProof(Verifier statement, Scalar witness, WasabiRandom random)
			=> CreateProof(statement, new ScalarVector(new[] { witness }), random);

		public static Proof CreateProof(Verifier statement, ScalarVector witness, WasabiRandom random)
			=> CreateProof(statement, new[] { witness }, random);

		public static Proof CreateProof(Verifier statement, IEnumerable<ScalarVector> witness, WasabiRandom random)
			=> CreateProof(statement.ToProver(witness), random);

		public static Verifier DiscreteLog(GroupElement publicPoint, GroupElement generator)
			=> Representation(publicPoint, generator);

		public static Verifier Representation(GroupElement publicPoint, params GroupElement[] generators)
			=> Representation(publicPoint, new GroupElementVector(generators));

		public static Verifier Representation(GroupElement publicPoint, GroupElementVector generators)
			=> new Verifier(LinearRelation(publicPoint, generators));

		private static Statement LinearRelation(GroupElement publicPoint, GroupElementVector generators)
			=> new Statement(new Equation(publicPoint, generators));
	}
}
