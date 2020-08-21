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
		public static KnowledgeOfRep CreateProof(Transcript transcript, KnowledgeOfRepParams parameters, WasabiRandom? random = null)
		{
			var statement = parameters.Statement;
			var t1 = transcript.Commit(statement);

			var nonce = GroupElement.Infinity;
			var randomScalars = transcript.GenerateNonces(parameters.Secrets, random);
			foreach (var (randomScalar, generator) in randomScalars.ZipForceEqualLength<Scalar, GroupElement>(parameters.Statement.Generators))
			{
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}
			var t2 = t1.Commit(nonce);

			var challenge = t2.GenerateChallenge().challenge;

			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in parameters.Secrets.ZipForceEqualLength(randomScalars))
			{
				Guard.False("secret == randomScalar", secret == randomScalar);
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			var proof = new KnowledgeOfRep(nonce, responses);

			// Sanity check:
			if (!Verifier.Verify(transcript, proof, statement))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}

			return proof;
		}

		public static KnowledgeOfDlog CreateProof(KnowledgeOfDlogParams parameters, WasabiRandom? random = null)
		{
			var proof = CreateProof(parameters as KnowledgeOfRepParams, random);

			return new KnowledgeOfDlog(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRep CreateProof(KnowledgeOfRepParams parameters, WasabiRandom? random = null)
			=> CreateProof(new Transcript(), parameters, random);
	}
}
