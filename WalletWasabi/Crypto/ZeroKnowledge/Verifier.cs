using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge.Transcripting;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class Verifier
	{
		public static bool Verify(Transcript transcript, KnowledgeOfRep proof, Statement statement)
		{
			var publicPoint = statement.PublicPoint;
			if (publicPoint == proof.Nonce)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(proof.Nonce)} should not be equal.");
			}

			var nonce = proof.Nonce;
			var responses = proof.Responses;

			var challenge = transcript
				.Commit(statement)
				.Commit(nonce)
				.GenerateChallenge()
				.challenge;

			var a = challenge * publicPoint + nonce;

			var b = GroupElement.Infinity;
			foreach (var (response, generator) in responses.ZipForceEqualLength(statement.Generators))
			{
				b += response * generator;
			}
			return a == b;
		}

		public static bool Verify(Transcript transcript, KnowledgeOfDlog proof, Statement statement)
			=> Verify(transcript, proof as KnowledgeOfRep, statement);

		public static bool Verify(KnowledgeOfRep proof, Statement statement)
			=> Verify(new Transcript(), proof, statement);
	}
}
