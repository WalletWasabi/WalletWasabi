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

		public static KnowledgeOfDiscreteLog CreateProof(Scalar secret, GroupElement publicPoint, GroupElement generator, WasabiRandom? random = null)
		{
			return CreateProof(new Transcript(), secret, publicPoint, generator, random);
		}

		public static KnowledgeOfDiscreteLog CreateProof(Transcript transcript, Scalar secret, GroupElement publicPoint, GroupElement generator, WasabiRandom? random = null)
		{
			var proof = CreateProof(transcript, new[] { (secret, generator) }, publicPoint, random);

			return new KnowledgeOfDiscreteLog(proof.Nonce, proof.Responses.First());
		}

		public static KnowledgeOfRepresentation CreateProof(IEnumerable<(Scalar secret, GroupElement generator)> secretGeneratorPairs, GroupElement publicPoint, WasabiRandom? random = null)
		{
			return CreateProof(new Transcript(), secretGeneratorPairs, publicPoint, random);
		}

		public static KnowledgeOfRepresentation CreateProof(Transcript transcript, IEnumerable<(Scalar secret, GroupElement generator)> secretGeneratorPairs, GroupElement publicPoint, WasabiRandom? random = null)
		{
			var generators = secretGeneratorPairs.Select(x => x.generator);
			var nonce = GroupElement.Infinity;
			var randomScalars = new List<Scalar>();
			var publicPointSanity = publicPoint;

			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);

			foreach (var (secret, generator) in secretGeneratorPairs)
			{
				Guard.False($"{nameof(secret)}.{nameof(secret.IsOverflow)}", secret.IsOverflow);
				Guard.False($"{nameof(secret)}.{nameof(secret.IsZero)}", secret.IsZero);
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
				publicPointSanity -= secret * generator;
			}

			if (publicPointSanity != GroupElement.Infinity)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} was incorrectly constructed.");
			}

			// before modifying anything, save a copy of the initial transcript state
			// for the verification check below
			var transcriptCopy = transcript.Clone();

			transcript.Statement(Encoding.UTF8.GetBytes(KnowledgeOfRepresentationTag), publicPoint, generators);

			foreach (var (secret, generator) in secretGeneratorPairs)
			{
				var randomScalar = transcript.GenerateNonce(secret, random);
				Guard.False("${nameof(randomScalar)}.{nameof(randomScalar.IsZero)}", randomScalar.IsZero);
				Guard.False("${nameof(randomScalar)} same as secret", randomScalar == secret);
				randomScalars.Add(randomScalar);
				var randomPoint = randomScalar * generator;
				nonce += randomPoint;
			}

			transcript.NonceCommitment(nonce);
			var challenge = transcript.GenerateChallenge();

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
			if (!Verifier.Verify(transcriptCopy, proof, publicPoint, generators))
			{
				throw new InvalidOperationException($"{nameof(CreateProof)} or {nameof(Verifier.Verify)} is incorrectly implemented. Proof was built, but verification failed.");
			}

			return proof;
		}
	}
}
