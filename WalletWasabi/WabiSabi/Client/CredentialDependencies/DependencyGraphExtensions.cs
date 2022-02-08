using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies;

public static class DependencyGraphExtensions
{
	/// <summary>Format the graph in graphviz dot format, suitable for
	/// reading or viewing.</summary>
	public static string AsGraphviz(this DependencyGraph g)
	{
		var output = "digraph {\n";

		Func<RequestNode, int> id = g.Vertices.IndexOf;

		foreach (var v in g.Vertices)
		{
			if (v.InitialBalance(CredentialType.Amount) == 0 && v.InitialBalance(CredentialType.Vsize) == 0)
			{
				output += $"  {id(v)} [label=\"\"];\n";
			}
			else
			{
				output += $"  {id(v)} [label=\"{v.InitialBalance(CredentialType.Amount)}s {v.InitialBalance(CredentialType.Vsize)}b\"];\n";
			}
		}

		foreach (var credentialType in DependencyGraph.CredentialTypes)
		{
			var color = credentialType == 0 ? "blue" : "red";
			var unit = credentialType == 0 ? "s" : "b";

			output += "  {\n";
			output += $"    edge [color={color}, fontcolor={color}];\n";

			foreach (var e in g.EdgeSets[credentialType].Predecessors.Values.Aggregate((a, b) => a.Union(b)).OrderByDescending(e => e.Value).ThenBy(e => id(e.From)).ThenBy(e => id(e.To)))
			{
				output += $"    {id(e.From)} -> {id(e.To)} [label=\"{e.Value}{unit}\"{(e.Value == 0 ? ", style=dashed" : "")}];\n";
			}

			output += "  }\n";
		}

		output += "}\n";
		return output;
	}
}
