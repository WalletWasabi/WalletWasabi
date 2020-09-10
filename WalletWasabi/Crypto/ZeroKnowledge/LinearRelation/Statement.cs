using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation
{
	public class Statement
	{
		public Statement(params Equation[] equations)
			: this(equations as IEnumerable<Equation>)
		{
		}

		public Statement(IEnumerable<Equation> equations)
		{
			// The equation matrix should not be jagged
			Guard.NotNullOrEmpty(nameof(equations), equations);
			var n = equations.First().Generators.Count();
			Guard.True(nameof(equations), equations.All(e => e.Generators.Count() == n));

			foreach (var generator in equations.SelectMany(equation => equation.Generators))
			{
				Guard.NotNull(nameof(generator), generator);
			}

			Equations = equations;
		}

		public Statement(GroupElement[,] equations)
		{
			// need to have at least one generator and one public point
			var terms = equations.GetLength(1);
			Guard.True(nameof(terms), terms >= 2);
			foreach (var generator in equations)
			{
				Guard.NotNull(nameof(generator), generator);
			}

			// make an equation out of each row taking the first element of each row as the public point
			var rows = Enumerable.Range(0, equations.GetLength(0));
			var cols = Enumerable.Range(1, terms - 1);
			Equations = rows.Select(i => new Equation(equations[i, 0], new GroupElementVector(cols.Select(j => equations[i, j]))));
		}

		public IEnumerable<Equation> Equations { get; }

		public IEnumerable<GroupElement> PublicPoints =>
			Equations.Select(x => x.PublicPoint);

		public IEnumerable<GroupElement> Generators =>
			Equations.SelectMany(x => x.Generators);

		public Knowledge ToKnowledge(ScalarVector witness) =>
			new Knowledge(this, witness);

		public bool CheckVerificationEquation(GroupElementVector publicNonces, Scalar challenge, ScalarVector responses)
		{
			// The responses matrix should match the generators in the equations and
			// there should be once nonce per equation.
			Guard.True(nameof(publicNonces), Equations.Count() == publicNonces.Count());

			return Equations.Zip(publicNonces, (equation, r) => equation.Verify(r, challenge, responses)).All(x => x);
		}
	}
}
