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
		public static KnowledgeOfDiscreteLog CreateProof(Scalar secret, Statement statement, WasabiRandom? random = null)
		{
			var proof = CreateProof(new Scalar[] { secret }, statement, random);

			return new KnowledgeOfDiscreteLog(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRepresentation CreateProof(IEnumerable<Scalar> secrets, Statement statement, WasabiRandom? random = null)
		{
			var secretsCount = secrets.Count();
			IEnumerable<GroupElement> generators = statement.Generators;
			var generatorsCount = generators.Count();
			if (secretsCount != generatorsCount)
			{
				const string NameofGenerators = nameof(generators);
				const string NameofSecrets = nameof(secrets);
				throw new InvalidOperationException($"Must provide exactly as many {NameofGenerators} as {NameofSecrets}. {NameofGenerators}: {generatorsCount}, {NameofSecrets}: {secretsCount}.");
			}

			var nonce = GroupElement.Infinity;
			var randomScalars = new List<Scalar>();
			var publicPointSanity = statement.PublicPoint;
			foreach (var (secret, generator) in secrets.ZipForceEqualLength<Scalar, GroupElement>(generators))
			{
				Guard.False($"{nameof(secret)}.{nameof(secret.IsOverflow)}", secret.IsOverflow);
				Guard.False($"{nameof(secret)}.{nameof(secret.IsZero)}", secret.IsZero);
				publicPointSanity -= secret * generator;

				var randomScalar = GetNonZeroRandomScalar(random);
				randomScalars.Add(randomScalar);
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}

			if (publicPointSanity != GroupElement.Infinity)
			{
				throw new InvalidOperationException($"{nameof(statement.PublicPoint)} was incorrectly constructed.");
			}

			var challenge = Challenge.Build(nonce, statement);

			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in secrets.ZipForceEqualLength(randomScalars))
			{
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			var proof = new KnowledgeOfRepresentation(nonce, responses);

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
