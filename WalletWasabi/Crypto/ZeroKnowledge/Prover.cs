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
		public const string KnowledgeOfRepresentationTag = "KnowledgeOfRepresentation"; // TODO add precise statement expression

		public static KnowledgeOfDiscreteLog CreateProof(Scalar secret, Statement statement, WasabiRandom? random = null)
		{
			return CreateProof(new Transcript(), secret, statement, random);
		}

		public static KnowledgeOfDiscreteLog CreateProof(Transcript transcript, Scalar secret, Statement statement, WasabiRandom? random = null)
		{
			var proof = CreateProof(new Scalar[] { secret }, statement, random);

			return new KnowledgeOfDiscreteLog(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRepresentation CreateProof(IEnumerable<Scalar> secrets, Statement statement, WasabiRandom? random = null)
		{
			return CreateProof(new Transcript(), secrets, statement, random);
		}

		public static KnowledgeOfRepresentation CreateProof(Transcript transcript, IEnumerable<Scalar> secrets, Statement statement, WasabiRandom? random = null)
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

			var publicPointSanity = statement.PublicPoint.Negate();

			foreach (var (secret, generator) in secrets.ZipForceEqualLength<Scalar, GroupElement>(generators))
			{
				Guard.False($"{nameof(secret)}.{nameof(secret.IsOverflow)}", secret.IsOverflow);
				Guard.False($"{nameof(secret)}.{nameof(secret.IsZero)}", secret.IsZero);
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
				publicPointSanity += secret * generator;
			}

			if (publicPointSanity != GroupElement.Infinity)
			{
				throw new InvalidOperationException($"{nameof(statement.PublicPoint)} was incorrectly constructed.");
			}

			// before modifying anything, save a copy of the initial transcript state
			// for the verification check below
			var transcriptCopy = transcript.Clone();

			transcript.Statement(statement);

			// TODO SPLIT HERE

			var nonce = GroupElement.Infinity;
			var randomScalars = transcript.GenerateNonces(secrets, random);
			foreach (var (randomScalar, generator) in randomScalars.ZipForceEqualLength<Scalar, GroupElement>(generators))
			{
				Guard.False("${nameof(randomScalar)}.${nameof(IsZero)}", randomScalar.IsZero);
				Guard.False("${nameof(randomScalar)}.IsOne", randomScalar == Scalar.One);
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}

			transcript.NonceCommitment(nonce);
			// TODO SPLIT HERE

			var challenge = transcript.GenerateChallenge();

			var responses = new List<Scalar>();
			foreach (var (secret, randomScalar) in secrets.ZipForceEqualLength(randomScalars))
			{
				Guard.False("secret == randomScalar", secret == randomScalar);
				var response = randomScalar + secret * challenge;
				responses.Add(response);
			}

			var proof = new KnowledgeOfRepresentation(nonce, responses);

			// Sanity check:
			if (!Verifier.Verify(transcriptCopy, proof, statement))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}

			return proof;
		}
	}
}
