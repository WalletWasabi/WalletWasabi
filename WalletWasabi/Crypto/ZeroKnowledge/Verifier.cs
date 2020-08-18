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
		public static bool Verify(KnowledgeOfRepresentation proof, Statement statement)
		{
			var publicPoint = statement.PublicPoint;
			if (publicPoint == proof.Nonce)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(proof.Nonce)} should not be equal.");
			}

			var nonce = proof.Nonce;
			var responses = proof.Responses;

			var challenge = Challenge.Build(nonce, statement);
			var a = challenge * publicPoint + nonce;

			var b = GroupElement.Infinity;
			foreach (var (response, generator) in responses.ZipForceEqualLength(statement.Generators))
			{
				b += response * generator;
			}
			return a == b;
		}

		public static bool Verify(KnowledgeOfDiscreteLog proof, Statement statement)
			=> Verify(proof as KnowledgeOfRepresentation, statement);
	}
}
