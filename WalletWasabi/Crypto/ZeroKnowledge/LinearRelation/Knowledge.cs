using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation
{
	public class Knowledge
	{
		public Knowledge(Statement statement, ScalarVector witness)
		{
			Guard.NotNull(nameof(statement), statement);
			Guard.NotNullOrEmpty(nameof(witness), witness);
			Guard.True(nameof(witness), witness.Count() == statement.Equations.First().Generators.Count(), $"{nameof(witness)} size does not match {nameof(statement)}.{nameof(statement.Equations)}");

			// don't try to prove something which isn't true
			foreach (var equation in statement.Equations)
			{
				Guard.True(nameof(witness), equation.VerifySolution(witness), $"{nameof(witness)} is not solution of the {nameof(equation)}");
			}

			Statement = statement;
			Witness = witness;
		}

		public Statement Statement { get; }
		public ScalarVector Witness { get; }

		public ScalarVector RespondToChallenge(Scalar challenge, ScalarVector secretNonces)
		{
			// allSecretNonces should have the same dimension as the equation matrix
			// Statement.Equations.CheckDimensions(allSecretNonces);

			return Statement.Equations.First().Respond(Witness, secretNonces, challenge); // FIXME refactor
		}
	}
}
