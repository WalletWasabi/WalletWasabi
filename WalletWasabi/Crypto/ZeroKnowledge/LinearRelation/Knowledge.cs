using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation
{
	internal class Knowledge
	{
		internal Knowledge(Statement statement, ScalarVector witness)
		{
			Guard.True(nameof(witness), witness.Count() == statement.Equations.First().Generators.Count(), $"{nameof(witness)} size does not match {nameof(statement)}.{nameof(statement.Equations)}");

			// don't try to prove something which isn't true
			foreach (var equation in statement.Equations)
			{
				equation.CheckSolution(witness);
			}

			Statement = statement;
			Witness = witness;
		}

		public Statement Statement { get; }
		public ScalarVector Witness { get; }

		internal ScalarVector RespondToChallenge(Scalar challenge, ScalarVector secretNonces) =>
			Equation.Respond(Witness, secretNonces, challenge);
	}
}
