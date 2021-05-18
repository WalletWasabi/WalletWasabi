using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class CredentialDependencyTests
	{
		[Fact]
		public void ResolveCredentialDependenciesBasic()
		{
			// Whitebox test of simple case, ensuring that edge values are
			// correct
			var inputValues = new ulong[][] { new[] { 3UL, 3UL } };
			var outputValues = new ulong[][] { new[] { 1UL, 1UL }, new[] { 1UL, 1UL }, new[] { 1UL, 1UL } };
			var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues);

			Assert.Equal(5, g.Vertices.Count);

			var edges = g.OutEdges(g.Vertices[0], CredentialType.Amount);

			Assert.Equal(4, edges.Count());
			Assert.Equal(2, edges.Where(x => x.Value > 0).Count());

			var small = edges.OrderByDescending(e => e.Value).Skip(1).First();
			Assert.Equal(1UL, small.Value);
			Assert.Equal(g.Vertices[3], small.To);
			Assert.Empty(g.OutEdges(small.To, CredentialType.Amount));
			Assert.Equal(2, g.InEdges(small.To, CredentialType.Amount).Count());
			Assert.Equal(1, g.InEdges(small.To, CredentialType.Amount).Select(e => e.From).Distinct().Count());
			Assert.Empty(g.OutEdges(small.To, CredentialType.VirtualBytes));
			Assert.Equal(2, g.InEdges(small.To, CredentialType.VirtualBytes).Count());
			Assert.Equal(g.Vertices[0], g.InEdges(small.To, CredentialType.VirtualBytes).Select(e => e.From).Distinct().Single());

			var large = edges.OrderByDescending(e => e.Value).First();
			Assert.Equal(2UL, large.Value);
			Assert.Equal(2, g.InEdges(large.To, CredentialType.Amount).Count());
			Assert.Equal(1, g.InEdges(large.To, CredentialType.Amount).Select(e => e.From).Distinct().Count());

			Assert.Equal(g.OutEdges(large.To, CredentialType.Amount).Select(e => e.To).ToHashSet(), g.Vertices.Skip(1).Take(2).ToHashSet());
			Assert.Equal(g.OutEdges(large.To, CredentialType.Amount).Where(e => e.Value == 0).Select(e => e.To).ToHashSet(), g.Vertices.Skip(1).Take(2).ToHashSet());
			Assert.Equal(g.OutEdges(large.To, CredentialType.Amount).Where(e => e.Value == 1).Select(e => e.To).ToHashSet(), g.Vertices.Skip(1).Take(2).ToHashSet());

			AssertResolvedGraphInvariants(g, inputValues, outputValues);
		}

		[Fact]
		public void ResolveCredentialDependenciesNoVsize()
		{
			var inputValues = new ulong[][] { new[] { 1UL, 0UL } };
			var outputValues = new ulong[][] { new[] { 1UL, 0UL } };
			var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues);

			Assert.Equal(2, g.Vertices.Count);

			var edges = g.OutEdges(g.Vertices[0], CredentialType.Amount);

			Assert.Equal(2, edges.Count());
			Assert.Equal(1, edges.Where(x => x.Value > 0).Count());

			var nonZeroEdge = edges.OrderByDescending(e => e.Value).First();
			Assert.Equal(1UL, nonZeroEdge.Value);
			Assert.Equal(g.Vertices[1], nonZeroEdge.To);
			Assert.Empty(g.OutEdges(nonZeroEdge.To, CredentialType.Amount));
			Assert.Equal(2, g.InEdges(nonZeroEdge.To, CredentialType.Amount).Count());
			Assert.Equal(1, g.InEdges(nonZeroEdge.To, CredentialType.Amount).Select(e => e.From).Distinct().Count());
			Assert.Empty(g.OutEdges(nonZeroEdge.To, CredentialType.VirtualBytes));
			Assert.Equal(2, g.InEdges(nonZeroEdge.To, CredentialType.VirtualBytes).Count());
			Assert.Equal(2, g.InEdges(nonZeroEdge.To, CredentialType.VirtualBytes).Select(e => e.Value == 0).Count());
			Assert.Equal(g.Vertices[0], g.InEdges(nonZeroEdge.To, CredentialType.VirtualBytes).Select(e => e.From).Distinct().Single());

			AssertResolvedGraphInvariants(g, inputValues, outputValues);
		}

		[Theory]
		[InlineData("1,1", "1,1", 2)]
		[InlineData("1,0", "1,0", 2)]
		[InlineData("2,0", "1,0", 2)]
		[InlineData("2,2", "1,1", 2)]
		[InlineData("2,2", "1,2", 2)]
		[InlineData("2,2", "2,2", 2)]
		[InlineData("2,2", "1,1 1,1", 3)]
		[InlineData("1,1 1,1", "2,2", 3)]
		[InlineData("1,1 1,1 1,1", "3,3", 5)]
		[InlineData("3,3", "1,1 1,1 1,1", 5)]
		[InlineData("1,0 1,0 1,0", "3,0", 5)]
		[InlineData("3,0", "1,0 1,0 1,0", 5)]
		[InlineData("1,5 1,5 1,5", "3,1", 5)]
		[InlineData("3,5", "1,1 1,1 1,1", 6)] // TODO 5?
		[InlineData("3,5", "1,1 1,1", 4)]
		[InlineData("4,0", "1,0 1,0 1,0 1,0", 7)]
		[InlineData("10,0", "1,0 1,0", 4)]
		[InlineData("10,0", "1,0 1,0 1,0", 6)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0", 8)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0", 10)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0 1,0", 12)]
		[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0", 19)]
		[InlineData("3,0 3,0", "2,0 2,0 2,0", 6)] // not 5 because of insufficient zero creds
		[InlineData("5,0 1,0", "2,0 2,0 2,0", 6)]
		[InlineData("2,0 2,0", "3,0", 3)]
		[InlineData("3,0 3,0", "2,0 2,0 1,0", 7)] // not 6 because of uneven amount requiring extra reissuance
		[InlineData("8,0 1,0", "3,0 3,0 3,0", 6)]
		[InlineData("8,0 2,0", "3,0 3,0 3,0", 6)]
		[InlineData("8,3 1,0", "3,1 3,1 3,1", 6)]
		[InlineData("8,3 2,0", "3,1 3,1 3,1", 6)]
		[InlineData("8,2 2,2", "3,1 3,1 3,1", 6)]
		[InlineData("3,0 1,0 1,0 1,0", "2,0 2,0 2,0", 7)]
		[InlineData("3,0 1,1 1,1 1,1", "2,1 2,1 2,1", 7)]
		[InlineData("3,3 1,0 1,0 1,0", "2,1 2,1 2,1", 8)]
		[InlineData("3,1 1,1 1,1 1,1", "2,1 2,1 2,1", 7)]
		[InlineData("3,3 1,1 1,1 1,1", "2,1 2,1 2,1", 7)]
		[InlineData("3,3 1,3 1,3 1,3", "2,1 2,1 2,1", 7)]
		[InlineData("3,2 1,0", "2,1 2,1", 4)]
		[InlineData("3,6 1,0", "2,1 2,1", 5)]
		[InlineData("3,6 1,6", "2,1 2,1", 4)]
		[InlineData("4,3", "1,1 1,1 1,1", 6)]
		[InlineData("3,6 1,0 1,0 1,0", "2,1 2,1 2,1", 9)]
		[InlineData("2,6 2,6", "1,3 1,3 1,3 1,3", 6)]
		[InlineData("2,6 2,6", "1,1 1,1 1,1 1,1", 8)]
		[InlineData("3,6 1,6", "4,1", 3)]
		[InlineData("3,6 1,6", "2,1 1,1 1,1", 6)] // TODO 5
		[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 1,1", 7)]
		[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 1,1 1,1", 8)]
		[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 2,1", 7)]
		[InlineData("5,6 1,6 1,6 1,6 1,6 1,6", "2,1 2,1 2,1 2,1 2,1", 12)]
		[InlineData("5,6 5,6 2,6 1,6 1,6 1,6", "8,1 3,1 2,1 2,1", 10)]
		[InlineData("20,1 5,6 5,6 2,6 2,6 1,6 1,6 1,6", "11,1 11,1 8,1 3,1 2,1 2,1", 15)]
		[InlineData("1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "21,21", 41)]
		[InlineData("21,21", "1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", 41)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,1 10,1 10,1 10,1", 41)]
		[InlineData("21,10 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,6 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,5 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 41)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 11,6", 42)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 11,5", 41)]
		[InlineData("20,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,6", 42)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "11,5 10,5 10,5 10,6", 42)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,6", 43)]
		[InlineData("21,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1 1,1", "10,5 10,5 10,5 10,5", 42)]
		public void ResolveCredentialDependencies(string inputs, string outputs, int finalVertexCount)
		{
			// blackbox tests (apart from finalVertexCount, which leaks
			// information about implementation) covering valid range
			// of inputs with various corner cases that must be handled.

			// Parse values out of strings because InputData can't contain arrays
			var inputValues = inputs.Split(" ").Select(x => x.Split(",").Select(y => ulong.Parse(y)));
			var outputValues = outputs.Split(" ").Select(x => x.Split(",").Select(y => ulong.Parse(y)));

			var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues);

			// Useful for debugging:
			// File.WriteAllText("/tmp/graphs/" + inputs + " -- " + outputs + ".dot", g.Graphviz());
			// the resulting graph can be rendered with graphviz (dot -Tpng -O *.dot)
			AssertResolvedGraphInvariants(g, inputValues, outputValues);
			Assert.Equal(finalVertexCount, g.Vertices.Count);

			// TODO when sum is even, reverse
		}

		private void AssertResolvedGraphInvariants(DependencyGraph graph, IEnumerable<IEnumerable<ulong>> inputValues, IEnumerable<IEnumerable<ulong>> outputValues)
		{
			for (CredentialType credentialType = 0; credentialType < CredentialType.NumTypes; credentialType++)
			{
				// Input nodes
				foreach (var node in graph.Vertices.Take(inputValues.Count()))
				{
					var balance = graph.Balance(node, credentialType);

					Assert.True(balance >= 0);
					Assert.Equal(0, node.MaxInDegree);
					Assert.Equal(node.MaxInDegree, graph.InDegree(node, credentialType));

					var outDegree = graph.OutDegree(node, credentialType);
					Assert.InRange(outDegree, 0, DependencyGraph.K - (balance == 0 ? 0 : 1)); // for amount creds, 1..K?
				}

				// Output nodes
				foreach (var node in graph.Vertices.Skip(inputValues.Count()).Take(outputValues.Count()))
				{
					var balance = graph.Balance(node, credentialType);

					Assert.Equal(0, balance);

					Assert.Equal(DependencyGraph.K, node.MaxInDegree);
					Assert.Equal(node.MaxInDegree, graph.InDegree(node, credentialType));

					Assert.Equal(0, node.MaxOutDegree);
					Assert.Equal(0, node.MaxZeroOnlyOutDegree);
					Assert.Equal(0, graph.OutDegree(node, credentialType));
				}

				// Reissuance nodes
				foreach (var node in graph.Vertices.Skip(inputValues.Count() + outputValues.Count()))
				{
					var balance = graph.Balance(node, credentialType);

					Assert.True(balance >= 0);

					Assert.Equal(DependencyGraph.K, node.MaxInDegree);
					Assert.Equal(node.MaxInDegree, graph.InDegree(node, credentialType));

					var outDegree = graph.OutDegree(node, credentialType);
					Assert.InRange(outDegree, 0, DependencyGraph.K - (balance == 0 ? 0 : 1));
				}
			}

			// TODO opportunistic draining of vsize credentials - add InlineData param for max depth?
			// TODO assert max depth < ceil(log count)?
		}

		[Fact]
		public void ResolveCredentialDependenciesThrows()
		{
			foreach ((var inputValues, var outputAmounts) in new (ulong[][], ulong[][])[]
			{
				(new[] { new[] { 1UL, 0UL } }, new[] { new[] { 2UL, 0UL } }),
				(new[] { Array.Empty<ulong>() }, new[] { new[] { 1UL, 0UL } }),
				(new[] { new[] { 1UL } }, new[] { new[] { 1UL, 0UL } }),
				(new[] { new[] { 1UL, 1UL, 1UL, } }, new[] { new[] { 1UL, 0UL } }),
				(new[] { new[] { 1UL, 0UL } }, new[] { new[] { 1UL } }),
				(new[] { new[] { 1UL, 0UL } }, new[] { new[] { 1UL, 0UL, 0UL } }),
			})
			{
				Assert.Throws<ArgumentException>(() => DependencyGraph.ResolveCredentialDependencies(inputValues, outputAmounts));
			}
		}
	}
}
