using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class Verifier
	{
		public static bool Verify(KnowledgeOfRepresentation proof, GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			return Verify(new Transcript(), proof, publicPoint, generators);
		}

		public static bool Verify(Transcript transcript, KnowledgeOfRepresentation proof, GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			Guard.NotNullOrEmpty(nameof(generators), generators);

			if (publicPoint == proof.Nonce)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(proof.Nonce)} should not be equal.");
			}

			foreach (var generator in generators)
			{
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			}

			var nonce = proof.Nonce;
			var responses = proof.Responses;

			transcript.Statement(Encoding.UTF8.GetBytes(Prover.KnowledgeOfRepresentationTag), publicPoint, generators);
			transcript.NonceCommitment(nonce);

			var challenge = transcript.GenerateChallenge();
			var a = challenge * publicPoint + nonce;

			var b = GroupElement.Infinity;
			foreach (var (response, generator) in responses.ZipForceEqualLength(generators))
			{
				b += response * generator;
			}
			return a == b;
		}

		public static bool Verify(KnowledgeOfDiscreteLog proof, GroupElement publicPoint, GroupElement generator, Transcript transcript)
			=> Verify(proof as KnowledgeOfRepresentation, publicPoint, generator);

		public static bool Verify(KnowledgeOfRepresentation proof, GroupElement publicPoint, params GroupElement[] generators)
			=> Verify(proof, publicPoint, generators as IEnumerable<GroupElement>);

		public static bool Verify(Transcript transcript, KnowledgeOfDiscreteLog proof, GroupElement publicPoint, GroupElement generator)
			=> Verify(transcript, proof as KnowledgeOfRepresentation, publicPoint, generator);

		public static bool Verify(Transcript transcript, KnowledgeOfRepresentation proof, GroupElement publicPoint, params GroupElement[] generators)
			=> Verify(transcript, proof, publicPoint, generators as IEnumerable<GroupElement>);
	}
}
