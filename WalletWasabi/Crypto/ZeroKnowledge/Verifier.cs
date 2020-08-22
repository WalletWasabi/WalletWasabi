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
			try
			{
				if (responses.Count() != statement.Generators.Count())
				{
					return false;
				}

				var a = challenge * statement.PublicPoint + nonce;
				var b = GroupElement.Infinity;
				foreach (var (response, generator) in responses.Zip(statement.Generators))
				{
					b += response * generator;
				}
				return a == b;
			}
			catch
			{
				return false;
			}
		}

		public static bool Verify(KnowledgeOfRep proof, Statement statement)
		{
			try
			{
				var nonce = proof.Nonce;
				var challenge = Challenge.Build(nonce, statement);
				return Verify(statement, nonce, proof.Responses, challenge);
			}
			catch
			{
				return false;
			}
		}

		public static bool Verify(KnowledgeOfAnd proof, IEnumerable<Statement> statements)
		{
			try
			{
				if (proof.KnowledgeOfRepresentations.Count() != statements.Count())
				{
					return false;
				}

				var challenge = Challenge.Build(proof.KnowledgeOfRepresentations.Select(x => x.Nonce), statements);

				return proof.KnowledgeOfRepresentations
					.Zip(statements)
					.All(x => Verify(x.Second, x.First.Nonce, x.First.Responses, challenge));
			}
			catch
			{
				return false;
			}
		}

		public static bool Verify(KnowledgeOfDlog proof, Statement statement)
			=> Verify(proof as KnowledgeOfRep, statement);
	}
}
