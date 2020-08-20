using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.Transcripting;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class Prover
	{
		public static KnowledgeOfDlog CreateProof(KnowledgeOfDlogParams parameters, WasabiRandom? random = null)
		{
			var proof = CreateProof(parameters as KnowledgeOfRepParams, random);

			return new KnowledgeOfDlog(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRep CreateProof(KnowledgeOfRepParams parameters, WasabiRandom? random = null)
		{
			return CreateProof(new Transcript(), parameters, random);
		}

		public static KnowledgeOfRep CreateProof(Transcript transcript, KnowledgeOfRepParams parameters, WasabiRandom? random = null)
		{
			// before modifying anything, save a copy of the initial transcript state
			// for the verification check below
			var transcriptCopy = transcript.Clone();

			var statement = parameters.Statement;
			transcript.Statement(statement);

			// TODO SPLIT HERE

			var nonce = GroupElement.Infinity;
			var randomScalars = transcript.GenerateNonces(parameters.Secrets, random);
			foreach (var (randomScalar, generator) in randomScalars.ZipForceEqualLength<Scalar, GroupElement>(parameters.Statement.Generators))
			{
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}
			transcript.NonceCommitment(nonce);

			// TODO SPLIT HERE

			var challenge = transcript.GenerateChallenge();

			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in parameters.Secrets.ZipForceEqualLength(randomScalars))
			{
				Guard.False("secret == randomScalar", secret == randomScalar);
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			var proof = new KnowledgeOfRep(nonce, responses);

			// Sanity check:
			if (!Verifier.Verify(transcriptCopy, proof, statement))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}

			return proof;
		}
	}
}
