using System.Collections.Generic;
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
			var value = (v is {Amount: 0, Vsize: 0}) ? "" : $"{v.Amount}s {v.Vsize}b";
			output += $"  {id(v)} [label=\"{value}\"];\n";
		}

		foreach (var credentialType in DependencyGraph.CredentialTypes)
		{
			var color = credentialType == 0 ? "blue" : "red";
			var unit = credentialType == 0 ? "s" : "b";

			output += "  {\n";
			output += $"    edge [color={color}, fontcolor={color}];\n";

			foreach (var e in g.EdgeSets[(int)credentialType].InEdges.Values.Aggregate((a, b) => a.Union(b)).OrderByDescending(e => e.Value).ThenBy(e => id(e.From)).ThenBy(e => id(e.To)))
			{
				output += $"    {id(e.From)} -> {id(e.To)} [label=\"{e.Value}{unit}\"{(e.Value == 0 ? ", style=dashed" : "")}];\n";
			}

			output += "  }\n";
		}

		output += "}\n";
		return output;
	}

	// The input nodes, in the order they were added
	public static IEnumerable<InputNode> GetInputs(this DependencyGraph me) => me.Vertices.OfType<InputNode>();
	// The output nodes, in the order they were added
	public static  IEnumerable<OutputNode> GetOutputs(this DependencyGraph me) => me.Vertices.OfType<OutputNode>();
	// The reissuance nodes, unsorted
	public static IEnumerable<ReissuanceNode> GetReissuances(this DependencyGraph me) => me.Vertices.OfType<ReissuanceNode>();

	public static IEnumerable<CredentialDependency> InEdges(this DependencyGraph me, RequestNode node, CredentialType credentialType) => me.EdgeSets[(int)credentialType].InEdges[node].OrderByDescending(e => e.Value);
	public static IEnumerable<CredentialDependency> OutEdges(this DependencyGraph me, RequestNode node, CredentialType credentialType) => me.EdgeSets[(int)credentialType].OutEdges[node].OrderByDescending(e => e.Value);
}
