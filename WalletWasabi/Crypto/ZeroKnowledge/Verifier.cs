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

			var challenge = Challenge.Build(publicPoint, nonce, generators);
			var a = challenge * publicPoint + nonce;

			var b = GroupElement.Infinity;
			foreach (var (response, generator) in responses.ZipForceEqualLength(generators))
			{
				b += response * generator;
			}
			return a == b;
		}

		public static bool Verify(KnowledgeOfAnd proof, IEnumerable<Statement> statements)
		{
			var repProofArray = proof.KnowledgeOfRepresentations.ToArray();
			var statementArray = statements.ToArray();
			var res = true;
			var challenge = Challenge.Build(statements, proof.KnowledgeOfRepresentations.Select(x => x.Nonce));
			for (int i = 0; i < repProofArray.Length; i++)
			{
				var repProof = repProofArray[i];
				var statement = statementArray[i];
				var nonce = repProof.Nonce;
				var responses = repProof.Responses;
				var publicPoint = statement.PublicPoint;
				var generators = statement.Generators;

				var a = challenge * publicPoint + nonce;

				var b = GroupElement.Infinity;
				foreach (var (response, generator) in responses.ZipForceEqualLength(generators))
				{
					b += response * generator;
				}
				res &= a == b;
			}
			return res;
		}

		public static bool Verify(KnowledgeOfDiscreteLog proof, GroupElement publicPoint, GroupElement generator)
			=> Verify(proof as KnowledgeOfRepresentation, publicPoint, generator);

		public static bool Verify(KnowledgeOfRepresentation proof, GroupElement publicPoint, params GroupElement[] generators)
			=> Verify(proof, publicPoint, generators as IEnumerable<GroupElement>);
	}
}
