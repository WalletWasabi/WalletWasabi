using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation
{
	internal record Knowledge
	{
		internal Knowledge(Statement statement, ScalarVector witness)
		{
			Guard.True(nameof(witness), witness.Count == statement.Equations.First().Generators.Count, $"{nameof(witness)} size does not match {nameof(statement)}.{nameof(statement.Equations)}");

			Statement = statement;
			Witness = witness;
		}

		public Statement Statement { get; }
		public ScalarVector Witness { get; }

		internal ScalarVector RespondToChallenge(Scalar challenge, ScalarVector secretNonces) =>
			Equation.Respond(Witness, secretNonces, challenge);

		/// <summary>For testing purposes.</summary>
		internal void AssertSoundness()
		{
			foreach (var equation in Statement.Equations)
			{
				equation.CheckSolution(Witness);
			}
		}
	}
}
