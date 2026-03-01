using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CredentialDependencyTests
{
	[Fact]
	public async Task AsyncDependencyGraphTraversalAsync()
	{
		var g = DependencyGraph.ResolveCredentialDependencies(
			inputValues: new[] { (10000L, 1930L), (1000L, 1930L) },
			outputValues: new[] { (5000L, 31L), (3500L, 31L), (2500L, 31L) },
			ProtocolConstants.MaxAmountPerAlice,
			ProtocolConstants.MaxVsizeCredentialValue);

		await SimulateAsyncRequestsAsync(g);
	}

	// Demonstrate how to use the dependency graph. Also checks it can be
	// executed with no deadlocks.
	private async Task SimulateAsyncRequestsAsync(DependencyGraph g)
	{
		// Keep track of the partial sets credentials to present. Requests
		// that are keys in this dictionary are still waiting to be sent.
		var pendingCredentialsToPresent = g.Vertices.ToDictionary(v => v, _ => DependencyGraph.CredentialTypes.ToDictionary(t => t, _ => new List<long>()));

		// A waiting request is blocked if either set of credentials that
		// need to be presented is incomplete. Since input registrations and
		// connection confirmation are modeled as a single node with in
		// degree 0, they are never blocked.
		ImmutableArray<RequestNode> UnblockedRequests() => pendingCredentialsToPresent.Keys.Where(node => DependencyGraph.CredentialTypes.All(t => g.InDegree(node, t) == pendingCredentialsToPresent[node][t].Count)).ToImmutableArray();

		// Also keep track of the in-flight requests
		var inFlightRequests = new List<(Task<ImmutableSortedDictionary<CredentialType, IEnumerable<long>>> Task, ImmutableSortedDictionary<CredentialType, IEnumerable<CredentialDependency>> Dependencies)>();

		// And all sent requests, for testing purposes.
		var sent = new HashSet<RequestNode>();

		// Simulate sending a request. Instead of actual Credential objects,
		// credentials are just represented as longs.
		async Task<ImmutableSortedDictionary<CredentialType, IEnumerable<long>>> SimulateRequest(
			RequestNode node,
			ImmutableSortedDictionary<CredentialType, IEnumerable<long>> presented,
			ImmutableSortedDictionary<CredentialType, IEnumerable<long>> requested)
		{
			foreach (var credentialType in presented.Keys)
			{
				Assert.Equal(g.InEdges(node, credentialType).Select(e => e.Value).OrderBy(x => x), presented[credentialType].OrderBy(x => x));
				Assert.Equal(g.OutEdges(node, credentialType).Select(e => e.Value).OrderBy(x => x), requested[credentialType].OrderBy(x => x));
			}

			Assert.DoesNotContain(node, sent);
			sent.Add(node);

			await Task.Delay(1 + Random.Shared.Next(10));

			return requested;
		}

		var ct = new CancellationTokenSource(new TimeSpan(0, 4, 0));

		for (var remainingSteps = 2 * pendingCredentialsToPresent.Count; remainingSteps > 0 && pendingCredentialsToPresent.Count + inFlightRequests.Count > 0; remainingSteps--)
		{
			// Clear unblocked but waiting requests. Not very efficient
			// (quadratic complexity), but good enough for demonstration
			// purposes.
			foreach (var node in UnblockedRequests())
			{
				// FIXME this is a little ugly, how should it look in the real code? seems like we're missing an abstraction
				var edgesByType = DependencyGraph.CredentialTypes.ToImmutableSortedDictionary(t => t, t => g.OutEdges(node, t));
				var credentialsToRequest = edgesByType.ToImmutableSortedDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(e => e.Value));
				var credentialsToPresent = pendingCredentialsToPresent[node].ToImmutableSortedDictionary(kvp => kvp.Key, kvp => kvp.Value.AsEnumerable());

				Assert.NotEmpty(edgesByType);

				var task = SimulateRequest(node, credentialsToPresent, credentialsToRequest);

				inFlightRequests.Add((Task: task, Dependencies: edgesByType));
				Assert.True(pendingCredentialsToPresent.Remove(node));
			}

			// At this point at least one task must be in progress.
			Assert.True(inFlightRequests.Count > 0);

			// Wait for a response to arrive
			var i = Task.WaitAny(inFlightRequests.Select(x => x.Task).ToArray(), ct.Token);
			Assert.InRange(i, 0, inFlightRequests.Count);
			var entry = inFlightRequests[i];
			inFlightRequests.RemoveAt(i);

			var issuedCredentials = await entry.Task;

			// Unblock the requests that depend on the issued credentials from this response
			foreach ((var credentialType, var edges) in entry.Dependencies)
			{
				Assert.Equal(edges.Count(), issuedCredentials[credentialType].Count());
				foreach ((var credential, var edge) in issuedCredentials[credentialType].Zip(edges))
				{
					// Ignore the fact that credential is the same as
					// edge.Value, it's meant to represent the real thing
					// since it's returned from the task.
					Assert.Equal(edge.Value, credential);
					pendingCredentialsToPresent[edge.To][credentialType].Add(credential);
				}
			}
		}

		Assert.Empty(inFlightRequests);
		Assert.Empty(pendingCredentialsToPresent);

		Assert.True(g.Vertices.All(v => sent.Contains(v)));

		ct.Dispose();
	}

	[Fact]
	public void ResolveCredentialDependenciesBasic()
	{
		// Whitebox test of simple case, ensuring that edge values are
		// correct
		var inputValues = new [] { (3L, 3L)};
		var outputValues = new [] { (1L, 1L), (1L, 1L), (1L, 1L)};
		var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues, ProtocolConstants.MaxAmountPerAlice, ProtocolConstants.MaxVsizeCredentialValue);

		Assert.Equal(5, g.Vertices.Count);

		var edges = g.OutEdges(g.Vertices[0], CredentialType.Amount);

		Assert.Equal(4, edges.Count());
		Assert.Equal(2, edges.Where(x => x.Value > 0).Count());

		var small = edges.OrderByDescending(e => e.Value).Skip(1).First();
		Assert.Equal(1L, small.Value);
		Assert.Equal(g.Vertices[3], small.To);
		Assert.Empty(g.OutEdges(small.To, CredentialType.Amount));
		Assert.Equal(2, g.InEdges(small.To, CredentialType.Amount).Count());
		Assert.Single(g.InEdges(small.To, CredentialType.Amount).Select(e => e.From).Distinct());
		Assert.Empty(g.OutEdges(small.To, CredentialType.Vsize));
		Assert.Equal(2, g.InEdges(small.To, CredentialType.Vsize).Count());
		Assert.Equal(g.Vertices[0], g.InEdges(small.To, CredentialType.Vsize).Select(e => e.From).Distinct().Single());

		var large = edges.OrderByDescending(e => e.Value).First();
		Assert.Equal(2L, large.Value);
		Assert.Equal(2, g.InEdges(large.To, CredentialType.Amount).Count());
		Assert.Single(g.InEdges(large.To, CredentialType.Amount).Select(e => e.From).Distinct());

		Assert.Equal(g.OutEdges(large.To, CredentialType.Amount).Select(e => e.To).ToHashSet(), g.Vertices.Skip(1).Take(2).ToHashSet());
		Assert.Equal(g.OutEdges(large.To, CredentialType.Amount).Where(e => e.Value == 0).Select(e => e.To).ToHashSet(), g.Vertices.Skip(1).Take(2).ToHashSet());
		Assert.Equal(g.OutEdges(large.To, CredentialType.Amount).Where(e => e.Value == 1).Select(e => e.To).ToHashSet(), g.Vertices.Skip(1).Take(2).ToHashSet());

		AssertResolvedGraphInvariants(g, inputValues, outputValues);
	}

	[Fact]
	public void ResolveCredentialDependenciesNoVsize()
	{
		var inputValues = new [] { (1L, 0L)};
		var outputValues = new [] { (1L, 0L)};
		var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues, ProtocolConstants.MaxAmountPerAlice, ProtocolConstants.MaxVsizeCredentialValue);

		Assert.Equal(2, g.Vertices.Count);

		var edges = g.OutEdges(g.Vertices[0], CredentialType.Amount);

		Assert.Equal(2, edges.Count());
		Assert.Single(edges, x => x.Value > 0);

		var nonZeroEdge = edges.OrderByDescending(e => e.Value).First();
		Assert.Equal(1L, nonZeroEdge.Value);
		Assert.Equal(g.Vertices[1], nonZeroEdge.To);
		Assert.Empty(g.OutEdges(nonZeroEdge.To, CredentialType.Amount));
		Assert.Equal(2, g.InEdges(nonZeroEdge.To, CredentialType.Amount).Count());
		Assert.Single(g.InEdges(nonZeroEdge.To, CredentialType.Amount).Select(e => e.From).Distinct());
		Assert.Empty(g.OutEdges(nonZeroEdge.To, CredentialType.Vsize));
		Assert.Equal(2, g.InEdges(nonZeroEdge.To, CredentialType.Vsize).Count());
		Assert.Equal(2, g.InEdges(nonZeroEdge.To, CredentialType.Vsize).Select(e => e.Value == 0).Count());
		Assert.Equal(g.Vertices[0], g.InEdges(nonZeroEdge.To, CredentialType.Vsize).Select(e => e.From).Distinct().Single());

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
	[InlineData("3,5", "1,1 1,1 1,1", 6)] // Can be improved to 5
	[InlineData("3,5", "1,1 1,1", 4)]
	[InlineData("4,0", "1,0 1,0 1,0 1,0", 7)]
	[InlineData("10,0", "1,0 1,0", 4)]
	[InlineData("10,0", "1,0 1,0 1,0", 6)]
	[InlineData("10,0", "1,0 1,0 1,0 1,0", 8)]
	[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0", 10)]
	[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0 1,0", 12)]
	[InlineData("10,0", "1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0 1,0", 19)]
	[InlineData("3,0 3,0", "2,0 2,0 2,0", 5)]
	[InlineData("5,0 1,0", "2,0 2,0 2,0", 6)]
	[InlineData("2,0 2,0", "3,0", 3)]
	[InlineData("3,0 3,0", "2,0 2,0 1,0", 6)]
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
	[InlineData("3,6 1,6", "2,1 1,1 1,1", 6)] // Can be improved to 5
	[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 1,1", 7)]
	[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 1,1 1,1", 8)]
	[InlineData("3,6 1,6 1,6 1,6", "2,1 2,1 2,1", 7)]
	[InlineData("5,6 1,6 1,6 1,6 1,6 1,6", "2,1 2,1 2,1 2,1 2,1", 12)]
	[InlineData("5,6 5,6 2,6 1,6 1,6 1,6", "8,1 3,1 2,1 2,1", 10)]
	[InlineData("6,6 6,6 3,6", "8,1 3,1 2,1 1,1 1,1", 10)]
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
	[InlineData("1,255 1,255 1,255 1,255", "4,1", 7)]
	[InlineData("13,255 1,255 1,255 1,255 1,255 1,255", "3,255 3,255 3,255 3,255 3,255 3,255", 17)]
	[InlineData("99991099,186 39991099,186 29991099,186 19991099,186 9991099,186", "33558431,31 33558431,31 33558431,31 33558431,31 33558431,31 28701813,31", 14)]
	[InlineData("99991099,186 39991099,186 29991099,186 19991099,186 9991099,186", "33558431,31 33558431,31 33558431,31 33558431,31 33558431,31 28701813,31 3192645,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 17121,31 12067,31", 50)]
	public async Task ResolveCredentialDependenciesAsync(string inputs, string outputs, int finalVertexCount)
	{
		// blackbox tests (apart from finalVertexCount, which leaks
		// information about implementation) covering valid range
		// of inputs with various corner cases that must be handled.

		// Parse values out of strings because InputData can't contain arrays
		long[] ParseTupla (string s) => s.Split(",").Select(long.Parse).ToArray();
		(long, long)[] ParseTuplas(string s) => s.Split(" ").Select(ParseTupla).Select(x => (x[0], x[1])).ToArray();
		var inputValues = ParseTuplas(inputs);
		var outputValues = ParseTuplas(outputs);

		var g = DependencyGraph.ResolveCredentialDependencies(inputValues, outputValues, ProtocolConstants.MaxAmountPerAlice, ProtocolConstants.MaxVsizeCredentialValue);

		// Useful for debugging:
		// File.WriteAllText("/tmp/graphs/" + inputs + " -- " + outputs + ".dot", g.Graphviz());
		// the resulting graph can be rendered with graphviz (dot -Tpng -O *.dot)
		AssertResolvedGraphInvariants(g, inputValues, outputValues);
		Assert.Equal(finalVertexCount, g.Vertices.Count);

		// TODO when sum(in) == sum(out) for all credential types, also
		// verify the reverse graph can be resolved.

		await SimulateAsyncRequestsAsync(g);
	}

	private void AssertResolvedGraphInvariants(DependencyGraph graph, IEnumerable<(long, long)> inputValues, IEnumerable<(long, long)> outputValues)
	{
		foreach (var credentialType in DependencyGraph.CredentialTypes)
		{
			// Input nodes
			foreach (var node in graph.Vertices.Take(inputValues.Count()))
			{
				var balance = graph.Balance(node, credentialType);

				Assert.True(balance >= 0);
				if (credentialType == CredentialType.Vsize)
				{
					Assert.InRange(balance, 0, ProtocolConstants.MaxVsizeCredentialValue);
				}
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
				if (credentialType == CredentialType.Vsize)
				{
					Assert.InRange(balance, 0, ProtocolConstants.MaxVsizeCredentialValue);
				}

				Assert.Equal(DependencyGraph.K, node.MaxInDegree);
				Assert.Equal(node.MaxInDegree, graph.InDegree(node, credentialType));

				var outDegree = graph.OutDegree(node, credentialType);
				Assert.InRange(outDegree, 0, DependencyGraph.K - (balance == 0 ? 0 : 1));
			}
		}

		// Ensure that vsize credentials do not exceed the range proof width
		foreach (var edge in graph.EdgeSets[(int)CredentialType.Vsize].OutEdges.Values.SelectMany(x => x))
		{
			Assert.InRange(edge.Value, 0, ProtocolConstants.MaxVsizeCredentialValue);
		}

		// TODO add InlineData param for max depth?

		// TODO assert max depth < ceil(log count)?
	}

	[Fact]
	public void EdgeConstraints()
	{
		var g = DependencyGraph.FromValues(new[] { ( 11L, 0L), (8L, 0L) }, new[] { (7L, 0L), (11L, 0L) }, ProtocolConstants.MaxAmountPerAlice, ProtocolConstants.MaxVsizeCredentialValue);

		var i = g.GetInputs().First();
		var o = g.GetOutputs().First();

		var edgeSet = g.EdgeSets[0];

		Assert.Equal(2, DependencyGraph.K);

		// insufficient out degree or in degree
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(o, i, 1));
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(o, i, 0));

		// insufficient out degree
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(o, o, 1));
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(o, o, 0));

		// insufficient in degree
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, i, 1));
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, i, 0));

		// excessive inwards/outwards value
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 12));

		// excessive inwards value
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 11));

		// excessive inwards value
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 7).AddEdge(i, o, 5));

		// max indegree exceeded
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 7).AddEdge(i, o, 0).AddEdge(i, o, 0));

		var o2 = g.GetOutputs().ElementAt(1);
		Assert.Equal(0, edgeSet.AddEdge(i, o2, 7).AddEdge(i, o2, 4).Balance(i));
		Assert.Equal(0, edgeSet.AddEdge(i, o2, 7).AddEdge(i, o2, 4).Balance(o2));
		Assert.Equal(0, edgeSet.AddEdge(i, o, 7).AddEdge(i, o, 0).AddEdge(i, o2, 4).Balance(i));
		Assert.Equal(1, edgeSet.AddEdge(i, o, 7).AddEdge(i, o, 0).AddEdge(i, o2, 0).RemainingOutDegree(i));
		Assert.Equal(1, edgeSet.AddEdge(i, o2, 11).AddEdge(i, o2, 0).AddEdge(i, o, 0).RemainingOutDegree(i));

		// insufficient outwards balance
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 1).AddEdge(i, o2, 11));

		// final in edge must discharge remaining negative balance
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 7).AddEdge(i, o, 0).AddEdge(i, o2, 4).AddEdge(i, o2, 0));

		// final out edge must discharge remaining balance
		Assert.Throws<InvalidOperationException>(() => edgeSet.AddEdge(i, o, 7).AddEdge(i, o2, 3));
	}
}

public static class CredentialDependencyExtensions
{
	public static long Balance(this DependencyGraph me, RequestNode node, CredentialType credentialType) => me.EdgeSets[(int)credentialType].Balance(node);
	public static int InDegree(this DependencyGraph me, RequestNode node, CredentialType credentialType) => me.EdgeSets[(int)credentialType].InDegree(node);
	public static int OutDegree(this DependencyGraph me, RequestNode node, CredentialType credentialType) => me.EdgeSets[(int)credentialType].OutDegree(node);
}
