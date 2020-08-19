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

		public static KnowledgeOfAnd CreateAndProof(KnowledgeOfRepParams[] knowledgeOfRepParams, WasabiRandom? random = null)
		{
			var (nonces, randomScalars) = GenerateNonces(knowledgeOfRepParams, random);
			var statements = knowledgeOfRepParams.Select(x => x.Statement);

			var challenge = Challenge.Build(nonces, statements);
			var andProof = CreateAndProof(knowledgeOfRepParams, nonces, randomScalars, challenge);

			// Sanity check:
			if (!Verifier.Verify(andProof, statements))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}

			return andProof;
		}

		public static KnowledgeOfRep CreateProof(KnowledgeOfRepParams parameters, WasabiRandom? random = null)
		{
			var (nonce, randomScalars) = GenerateNonces(parameters, random);
			var statement = parameters.Statement;
			var challenge = Challenge.Build(nonce, statement);
			KnowledgeOfRep proof = CreateProof(parameters, nonce, randomScalars, challenge);

			// Sanity check:
			if (!Verifier.Verify(proof, statement))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}
			return proof;
		}

		private static KnowledgeOfAnd CreateAndProof(KnowledgeOfRepParams[] knowledgeOfRepParams, IEnumerable<GroupElement> nonces, IEnumerable<IEnumerable<Scalar>> randomScalars, Scalar challenge)
		{
			var knowledgeOfRepresentations = new List<KnowledgeOfRep>();

			var paramsArray = knowledgeOfRepParams.ToArray();
			var scalarsArray = randomScalars.ToArray();
			var noncesArray = nonces.ToArray();
			for (int i = 0; i < paramsArray.Length; i++)
			{
				var parameters = paramsArray[i];
				var scalars = scalarsArray[i];
				var nonce = noncesArray[i];

				var proof = CreateProof(parameters, nonce, scalars, challenge);
				knowledgeOfRepresentations.Add(proof);
			}

			return new KnowledgeOfAnd(knowledgeOfRepresentations);
		}

		private static KnowledgeOfRep CreateProof(KnowledgeOfRepParams parameters, GroupElement nonce, IEnumerable<Scalar> randomScalars, Scalar challenge)
		{
			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in parameters.Secrets.ZipForceEqualLength(randomScalars))
			{
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			return new KnowledgeOfRep(nonce, responses);
		}

		private static (IEnumerable<GroupElement> nonces, IEnumerable<IEnumerable<Scalar>> randomScalars) GenerateNonces(IEnumerable<KnowledgeOfRepParams> parameters, WasabiRandom? random = null)
		{
			var nonces = new List<GroupElement>();
			var randomScalars = new List<IEnumerable<Scalar>>();
			foreach (var parameter in parameters)
			{
				var (nonce, scalars) = GenerateNonces(parameter, random);
				nonces.Add(nonce);
				randomScalars.Add(scalars);
			}

			return (nonces, randomScalars);
		}

		private static (GroupElement nonce, IEnumerable<Scalar> randomScalars) GenerateNonces(KnowledgeOfRepParams parameters, WasabiRandom? random = null)
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

			return (nonce, randomScalars);
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
