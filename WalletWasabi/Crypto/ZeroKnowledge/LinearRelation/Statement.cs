using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;

internal class Statement
{
	private static GroupElement O = GroupElement.Infinity;

	internal Statement(GroupElement publicPoint, IEnumerable<GroupElement> generators)
		: this(ToTable(generators.Prepend(publicPoint)))
	{
	}

	internal Statement(params GroupElement[] equation)
		: this(ToTable(equation))
	{
	}

	internal Statement(GroupElement[,] equations)
	{
		var terms = equations.GetLength(1);
		Guard.True(nameof(terms), terms >= 2, $"Invalid {nameof(terms)}. It needs to have at least one generator and one public point.");

		// make an equation out of each row taking the first element of each row as the public point
		var rows = Enumerable.Range(0, equations.GetLength(0));
		var cols = Enumerable.Range(1, terms - 1);
		Equations = rows.Select(i => new Equation(equations[i, 0] ?? O, new GroupElementVector(cols.Select(j => equations[i, j] ?? O))));
	}

	internal IEnumerable<Equation> Equations { get; }

	public IEnumerable<GroupElement> PublicPoints =>
		Equations.Select(x => x.PublicPoint);

	public IEnumerable<GroupElement> Generators =>
		Equations.SelectMany(x => x.Generators);

	public bool CheckVerificationEquation(GroupElementVector publicNonces, Scalar challenge, ScalarVector responses)
	{
		// The responses matrix should match the generators in the equations and
		// there should be once nonce per equation.
		Guard.True(nameof(publicNonces), Equations.Count() == publicNonces.Count);

		return Equations.Zip(publicNonces, (equation, r) => equation.Verify(r, challenge, responses)).All(x => x);
	}

	// Helper function for constructor
	private static GroupElement[,] ToTable(IEnumerable<GroupElement> equation)
	{
		var table = new GroupElement[1, equation.Count()];

		foreach (var (g, i) in equation.Select((x, i) => (x, i)))
		{
			table[0, i] = g;
		}

		return table;
	}
}
