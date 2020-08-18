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
		public static KnowledgeOfDiscreteLog CreateProof(Scalar secret, GroupElement publicPoint, GroupElement generator, WasabiRandom? random = null)
		{
			var proof = CreateProof(new KnowledgeOfRepresentationParameters(new[] { (secret, generator) }, publicPoint), random);

			return new KnowledgeOfDiscreteLog(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRepresentation CreateProof(KnowledgeOfRepresentationParameters parameters, WasabiRandom? random = null)
		{
			var publicPoint = parameters.PublicPoint;
			var secretGeneratorPairs = parameters.SecretGeneratorPairs;
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);

			var nonce = GroupElement.Infinity;
			var randomScalars = new List<Scalar>();
			var publicPointSanity = publicPoint;
			foreach (var (secret, generator) in secretGeneratorPairs)
			{
				Guard.False($"{nameof(secret)}.{nameof(secret.IsOverflow)}", secret.IsOverflow);
				Guard.False($"{nameof(secret)}.{nameof(secret.IsZero)}", secret.IsZero);
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
				publicPointSanity -= secret * generator;

				var randomScalar = GetNonZeroRandomScalar(random);
				randomScalars.Add(randomScalar);
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}

			if (publicPointSanity != GroupElement.Infinity)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} was incorrectly constructed.");
			}

			var generators = secretGeneratorPairs.Select(x => x.generator);
			var challenge = Challenge.Build(publicPoint, nonce, generators);

			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in secretGeneratorPairs
				.Select(x => x.secret)
				.ZipForceEqualLength(randomScalars))
			{
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			var proof = new KnowledgeOfRepresentation(nonce, responses);

			// Sanity check:
			if (!Verifier.Verify(proof, publicPoint, generators))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}
			return proof;
		}

		public static KnowledgeOfAnd CreateAndProof(IEnumerable<KnowledgeOfRepresentationParameters> knowledgeOfRepresentationParameters, WasabiRandom? random = null)
		{
			var nonces = new List<GroupElement>();
			var randomScalarsScalars = new List<IEnumerable<Scalar>>();
			var statements = new List<Statement>();
			foreach (var korp in knowledgeOfRepresentationParameters)
			{
				var publicPoint = korp.PublicPoint;
				var secretGeneratorPairs = korp.SecretGeneratorPairs;
				Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);

				var nonce = GroupElement.Infinity;
				var randomScalars = new List<Scalar>();
				var publicPointSanity = publicPoint;
				foreach (var (secret, generator) in secretGeneratorPairs)
				{
					Guard.False($"{nameof(secret)}.{nameof(secret.IsOverflow)}", secret.IsOverflow);
					Guard.False($"{nameof(secret)}.{nameof(secret.IsZero)}", secret.IsZero);
					Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
					publicPointSanity -= secret * generator;

					var randomScalar = GetNonZeroRandomScalar(random);
					randomScalars.Add(randomScalar);
					var randomPoint = randomScalar * generator;
					nonce += randomPoint;
				}

				if (publicPointSanity != GroupElement.Infinity)
				{
					throw new InvalidOperationException($"{nameof(publicPoint)} was incorrectly constructed.");
				}

				nonces.Add(nonce);
				randomScalarsScalars.Add(randomScalars);
				statements.Add(new Statement(korp.PublicPoint, korp.SecretGeneratorPairs.Select(x => x.generator)));
			}

			var challenge = Challenge.Build(statements, nonces);

			var knowledgeOfRepresentations = new List<KnowledgeOfRepresentation>();
			KnowledgeOfRepresentationParameters[] array = knowledgeOfRepresentationParameters.ToArray();
			var randomScalarsScalarsArray = randomScalarsScalars.ToArray();
			var noncesArray = nonces.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				var korp = array[i];
				var randomScalars = randomScalarsScalarsArray[i];
				var nonce = nonces[i];
				var responses = new List<Scalar>();
				foreach (var (secret, randomScalar) in korp.SecretGeneratorPairs
					.Select(x => x.secret)
					.ZipForceEqualLength(randomScalars))
				{
					var response = randomScalar + secret * challenge;
					responses.Add(response);
				}

				var proof = new KnowledgeOfRepresentation(nonce, responses);
				knowledgeOfRepresentations.Add(proof);
			}

			var proofOfKnowledge = new KnowledgeOfAnd(knowledgeOfRepresentations);

			// Sanity check:
			if (!Verifier.Verify(proofOfKnowledge, statements))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}

			return proofOfKnowledge;
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
