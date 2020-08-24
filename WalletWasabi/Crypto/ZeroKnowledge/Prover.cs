using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
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
			var nonce = GroupElement.Infinity;
			var randomScalars = new List<Scalar>();
			foreach (var (secret, generator) in parameters.SecretGeneratorPairs)
			{
				var randomScalar = GetNonZeroRandomScalar(random);
				randomScalars.Add(randomScalar);
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}

			var statement = parameters.Statement;
			var challenge = Challenge.Build(nonce, statement);

			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in parameters.Secrets.ZipForceEqualLength(randomScalars))
			{
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			var proof = new KnowledgeOfRep(nonce, responses);

			// Sanity check:
			if (!Verifier.Verify(proof, statement))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}
			return proof;
		}

		private static Scalar GetNonZeroRandomScalar(WasabiRandom? random = null)
		{
			var disposeRandom = false;
			if (random is null)
			{
				disposeRandom = true;
				random = new SecureRandom();
			}

			try
			{
				var scalar = random.GetScalar(allowZero: false);

				// Sanity checks:
				if (scalar.IsOverflow || scalar.IsZero)
				{
					throw new InvalidOperationException("Bloody murder! Random generator served invalid scalar.");
				}

				return scalar;
			}
			finally
			{
				if (disposeRandom)
				{
					(random as IDisposable)?.Dispose();
				}
			}
		}
	}
}
