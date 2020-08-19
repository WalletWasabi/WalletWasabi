using NBitcoin.Secp256k1;
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
		private static bool Verify(Statement statement, GroupElement nonce, IEnumerable<Scalar> responses, Scalar challenge)
		{
			var publicPoint = statement.PublicPoint;
			if (publicPoint == nonce)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(nonce)} should not be equal.");
			}

			var a = challenge * statement.PublicPoint + nonce;

			var b = GroupElement.Infinity;
			foreach (var (response, generator) in responses.ZipForceEqualLength(statement.Generators))
			{
				b += response * generator;
			}
			return a == b;
		}

		public static bool Verify(KnowledgeOfRep proof, Statement statement)
		{
			var nonce = proof.Nonce;
			var challenge = Challenge.Build(nonce, statement);
			return Verify(statement, nonce, proof.Responses, challenge);
		}

		public static bool Verify(KnowledgeOfAnd proof, IEnumerable<Statement> statements)
		{
			var challenge = Challenge.Build(proof.KnowledgeOfRepresentations.Select(x => x.Nonce), statements);
			return proof.KnowledgeOfRepresentations
				.ZipForceEqualLength(statements)
				.All(x => Verify(x.Item2, x.Item1.Nonce, x.Item1.Responses, challenge));
		}

		public static bool Verify(KnowledgeOfDlog proof, Statement statement)
			=> Verify(proof as KnowledgeOfRep, statement);
	}
}
